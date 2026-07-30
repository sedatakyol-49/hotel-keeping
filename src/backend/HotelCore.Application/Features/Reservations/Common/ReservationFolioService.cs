using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Folio (açık hesap) yönetimi — architecture.md §4.3: konaklama boyunca masraflar folioda
/// birikir, check-out'ta faturaya dönüşür.
/// <para>
/// Bu fazda folio <b>tek bir konaklama satırı</b> (<c>RoomCharge</c>) ile açılır; ekstra
/// harcamalar (minibar, kahvaltı) ve <c>CityTax</c> (Kurtaxe) satırları faturalama modülüyle
/// gelecektir. Fatura henüz oluşmadığı için <c>InvoiceId = null</c>'dır ve satır serbestçe
/// güncellenebilir (GoBD değiştirilemezlik guard'ı yalnızca faturaya bağlı satırlara uygular).
/// </para>
/// <para>
/// <b>KDV:</b> konaklama satırına otelin <b>indirimli</b> oranı uygulanır
/// (<c>Hotel.TaxProfile.ReducedVatRate</c> — DE: %7). Oran koda hardcode edilmez
/// (architecture.md §4.1) ve satıra <b>kopyalanır</b>: otelin oranı sonradan değişse bile
/// mevcut belge değişmez.
/// </para>
/// </summary>
internal sealed class ReservationFolioService(IAppDbContext database)
{
    /// <summary>
    /// Rezervasyon için folioyu (yoksa) açar ve konaklama satırını istenen tutara göre
    /// oluşturur/güncelleştirir. <c>SaveChanges</c> çağrılmaz — çağıran handler kaydı tek
    /// transaction içinde yazar.
    /// </summary>
    /// <param name="reservation">Kaydedilmiş (Id'si olan) rezervasyon.</param>
    /// <param name="nights">Gece sayısı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public async Task SyncRoomChargeAsync(
        Reservation reservation,
        int nights,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var folio = await database.Folios
            .FirstOrDefaultAsync(candidate => candidate.ReservationId == reservation.Id, cancellationToken)
            .ConfigureAwait(false);

        if (folio is null)
        {
            folio = new Folio
            {
                HotelId = reservation.HotelId,
                ReservationId = reservation.Id,
                IsClosed = false,
            };

            database.Folios.Add(folio);
        }

        var vatRate = await database.Hotels
            .Where(hotel => hotel.Id == reservation.HotelId)
            .Select(hotel => hotel.TaxProfile.ReducedVatRate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var line = await database.InvoiceLineItems
            .FirstOrDefaultAsync(
                candidate => candidate.FolioId == folio.Id
                             && candidate.InvoiceId == null
                             && candidate.Type == InvoiceLineType.RoomCharge,
                cancellationToken)
            .ConfigureAwait(false);

        if (line is null)
        {
            line = new InvoiceLineItem
            {
                HotelId = reservation.HotelId,
                Folio = folio,
                Type = InvoiceLineType.RoomCharge,
                SortOrder = 0,
            };

            database.InvoiceLineItems.Add(line);
        }

        var gross = reservation.TotalAmount;

        // Brüt tutardan net/KDV ayrıştırılır: fiyat planları ve BasePrice brüt (misafirin
        // gördüğü) tutarlardır. Yuvarlama satır bazında yapılır ki net + KDV = brüt olsun.
        var net = Math.Round(gross / (1 + (vatRate / 100m)), 2, MidpointRounding.AwayFromZero);

        line.Description = $"Ubernachtung {reservation.CheckIn:yyyy-MM-dd} - {reservation.CheckOut:yyyy-MM-dd}";
        line.Quantity = nights;

        // Gecelik birim fiyat gösterim içindir; kesin toplam LineNet + LineVat'tır (sezon
        // geçişinde geceler farklı fiyatlanabildiği için birim fiyat ortalamadır).
        line.UnitPrice = nights > 0
            ? Math.Round(gross / nights, 2, MidpointRounding.AwayFromZero)
            : gross;
        line.VatRate = vatRate;
        line.LineNet = net;
        line.LineVat = gross - net;
        line.ServiceDate = reservation.CheckIn;
    }
}
