using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Services;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Raporların <b>tek veri erişim noktası</b>. Her metot <b>tek</b> ve <b>toplulaştırılmış</b>
/// (<c>GROUP BY</c> + <c>SUM</c>/<c>COUNT</c>) bir SQL sorgusu çalıştırır.
///
/// <para><b>Performans sözü:</b> hiçbir sorgu rezervasyon/fatura satırlarını tek tek belleğe
/// çekmez ve hiçbir yerde döngü içinde sorgu yoktur (N+1 yok). Sorgu sayısı sabittir:
/// doluluk raporu <b>3</b>, ciro raporu <b>6</b> sorgu.</para>
///
/// <para><b>Günlük seri nasıl üretiliyor:</b> PostgreSQL <c>generate_series</c> gibi bir takvim
/// üreteci EF Core üzerinden <c>IAppDbContext</c> portuyla çağrılamaz (ham SQL Infrastructure'a
/// aittir). Bunun yerine gün listesi <b>bellekte</b> üretilir
/// (<see cref="ReportPeriod"/>, en fazla 366 eleman) ve SQL'den gelen <b>kova</b> toplamları
/// (<see cref="StayGroupRow"/>) bu diziye yayılır. Kova anahtarı <c>(giriş, çıkış)</c> içerdiği
/// için hangi günlere yayılacağı bellekte bilinir; yayma maliyeti toplam oda-gece ile sınırlıdır.
/// Yani: <b>toplamlar SQL'de</b>, <b>gün ekseni bellekte</b>.</para>
///
/// <para><b>Tenant izolasyonu:</b> tüm sorgular global query filter'ın üzerine <b>ek olarak</b>
/// <c>hotelIds</c> kısıtı uygular. Bu bir bypass değil, <b>daraltmadır</b>: konsolide modda
/// global filter tüm otellere açılır (<c>|| CanAccessAllHotels</c>), oysa rapor yalnızca
/// kullanıcının <see cref="HotelReader.AccessibleHotels"/> ile doğrulanmış — yani kendi Head
/// Office'ine bağlı — otellerini kapsamalıdır.</para>
/// </summary>
internal sealed class ReportDataSource(
    IAppDbContext database,
    ICurrentUser currentUser,
    HotelReader hotels)
{
    /// <summary>
    /// Aktif otel seçili mi? <c>false</c> ise Head Office kullanıcısı <c>X-Hotel-Id</c>
    /// göndermemiştir → <b>konsolide</b> mod. Rapor uçları bu durumda hata vermez (bkz.
    /// <c>ReportReader</c> sınıf yorumu, "konsolide mod kararı").
    /// </summary>
    public bool IsSingleHotelScope => currentUser.HotelId is not null;

    /// <summary>
    /// Raporun kapsadığı oteller. Aktif otel seçiliyse (<c>X-Hotel-Id</c> veya kullanıcının
    /// varsayılan oteli) yalnızca o otel; Head Office kullanıcısı aktif otel seçmemişse
    /// erişilebilir <b>tüm</b> oteller (konsolide mod).
    /// </summary>
    public async Task<IReadOnlyList<ReportHotelInfo>> GetScopeHotelsAsync(CancellationToken cancellationToken)
    {
        var accessible = await hotels.AccessibleHotels()
            .OrderBy(hotel => hotel.Name)
            .Select(hotel => new ReportHotelInfo(hotel.Id, hotel.Name, hotel.Currency))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return currentUser.HotelId is Guid activeHotelId
            ? accessible.Where(hotel => hotel.Id == activeHotelId).ToList()
            : accessible;
    }

    /// <summary>
    /// Otel bazında oda sayısı ve servis dışı oda sayısı.
    /// <para>
    /// <b>Not:</b> <c>IsOutOfOrder</c> tarih aralığı taşımayan <i>anlık</i> bir durumdur; bu
    /// yüzden servis dışılık tüm rapor dönemine uygulanır (bkz. <see cref="ReportDefinitions"/>
    /// §8). Soft-delete edilmiş odalar global filtre gereği hiç sayılmaz.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<RoomCapacityRow>> GetRoomCapacityAsync(
        Guid[] hotelIds,
        CancellationToken cancellationToken) =>
        await database.Rooms
            .Where(room => hotelIds.Contains(room.HotelId))
            .GroupBy(room => room.HotelId)
            .Select(group => new RoomCapacityRow(
                group.Key,
                group.Count(),
                group.Count(room => room.IsOutOfOrder)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Rapor penceresiyle kesişen ve odayı <b>bloke eden</b> rezervasyonlar
    /// (<c>Cancelled</c>/<c>NoShow</c> hariç — kural <see cref="AvailabilityQuery"/>'de tek
    /// yerdedir), <c>(otel, giriş, çıkış, kanal)</c> bazında toplanmış.
    /// </summary>
    /// <param name="hotelIds">Kapsamdaki otel kimlikleri.</param>
    /// <param name="period">Rapor dönemi.</param>
    /// <param name="onlyBilled">
    /// <c>true</c> ise yalnızca <b>en az bir kez kesinleşmiş faturası olan</b> konaklamalar.
    /// İki çağrının farkı "henüz faturalanmamış konaklama tutarı"nı verir
    /// (<c>unbilledRoomRevenueGross</c>); bu, tek sorguda koşullu toplam yazmaktan hem daha
    /// okunur hem de EF çevirisi açısından daha güvenlidir.
    /// </param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public async Task<IReadOnlyList<StayGroupRow>> GetStayGroupsAsync(
        Guid[] hotelIds,
        ReportPeriod period,
        bool onlyBilled,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var query = database.Reservations
            .Where(reservation => hotelIds.Contains(reservation.HotelId))
            .BlockingBetween(period.From, period.NightEndExclusive);

        if (onlyBilled)
        {
            // "Faturalanmis" = bir kez numara almis (IssuedAt dolu) faturasi var.
            // Taslak ve taslakken iptal edilmis faturalar SAYILMAZ (bkz. RevenueRecognition).
            query = query.Where(reservation =>
                reservation.Invoices.Any(invoice => invoice.IssuedAt != null));
        }

        return await query
            .GroupBy(reservation => new
            {
                reservation.HotelId,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Channel,
            })
            .Select(group => new StayGroupRow(
                group.Key.HotelId,
                group.Key.CheckIn,
                group.Key.CheckOut,
                group.Key.Channel,
                group.Count(),
                group.Sum(reservation => reservation.TotalAmount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Konaklamaya bağlı <b>kesinleşmiş</b> fatura satırlarının kova + tür bazında toplamı.
    /// <para>
    /// Filtre <c>Invoice.IssuedAt != null</c>'dır: taslaklar ve taslakken iptal edilenler
    /// dışarıda kalır; kesinleştikten sonra iptal edilen fatura ile onun Stornorechnung'u
    /// <b>ikisi de</b> içeride kalır ve birbirini sıfırlar (bkz. <see cref="RevenueRecognition"/>).
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<StayMoneyRow>> GetStayInvoiceAmountsAsync(
        Guid[] hotelIds,
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var from = period.From;
        var nightEndExclusive = period.NightEndExclusive;

        return await database.InvoiceLineItems
            .Where(line => hotelIds.Contains(line.HotelId)
                && line.Invoice != null
                && line.Invoice.IssuedAt != null
                && line.Invoice.Reservation != null
                && line.Invoice.Reservation.Status != ReservationStatus.Cancelled
                && line.Invoice.Reservation.Status != ReservationStatus.NoShow
                && line.Invoice.Reservation.CheckIn < nightEndExclusive
                && from < line.Invoice.Reservation.CheckOut)
            .GroupBy(line => new
            {
                line.Invoice!.Reservation!.HotelId,
                line.Invoice!.Reservation!.CheckIn,
                line.Invoice!.Reservation!.CheckOut,
                line.Invoice!.Reservation!.Channel,
                line.Type,
            })
            .Select(group => new StayMoneyRow(
                group.Key.HotelId,
                group.Key.CheckIn,
                group.Key.CheckOut,
                group.Key.Channel,
                group.Key.Type,
                group.Sum(line => line.LineNet),
                group.Sum(line => line.LineVat)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Konaklama gecelerine dağıtılamayan kesinleşmiş fatura satırları (elle kesilen faturalar +
    /// iptal/no-show rezervasyona bağlı faturalar), <b>Leistungsdatum</b>'a göre dönemlenmiş.
    /// <para>
    /// <c>ServiceDate</c> boş olan satır <b>sayılmaz</b>: hizmet tarihi GoBD zorunlu alanıdır ve
    /// sunucu tarafından her zaman doldurulur; boş bir satırı keyfî bir güne yazmak yerine
    /// raporun dışında bırakmak dürüst davranıştır.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<OtherInvoiceRow>> GetOtherInvoiceAmountsAsync(
        Guid[] hotelIds,
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var from = period.From;
        var to = period.To;

        return await database.InvoiceLineItems
            .Where(line => hotelIds.Contains(line.HotelId)
                && line.Invoice != null
                && line.Invoice.IssuedAt != null
                && (line.Invoice.Reservation == null
                    || line.Invoice.Reservation.Status == ReservationStatus.Cancelled
                    || line.Invoice.Reservation.Status == ReservationStatus.NoShow)
                && line.ServiceDate != null
                && line.ServiceDate >= from
                && line.ServiceDate <= to)
            .GroupBy(line => new { line.HotelId, line.Type })
            .Select(group => new OtherInvoiceRow(
                group.Key.HotelId,
                group.Key.Type,
                group.Sum(line => line.LineNet),
                group.Sum(line => line.LineVat)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
