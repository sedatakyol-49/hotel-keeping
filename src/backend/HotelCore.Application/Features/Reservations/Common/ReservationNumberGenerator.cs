using System.Globalization;
using HotelCore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Misafire iletilen okunur rezervasyon kodunu üretir: <c>RES-{yıl}-{5 haneli sıra}</c>
/// (örn. <c>RES-2026-00042</c>).
/// <para>
/// <b>Üretim yöntemi:</b> aynı otelde, aynı yılın önekiyle başlayan <b>en büyük</b> numara
/// okunur ve bir artırılır. Sıra numarası <b>sabit 5 hane</b> sıfır dolgulu olduğu için
/// sözlük sırası = sayısal sıra; bu yüzden <c>ORDER BY reservation_number DESC LIMIT 1</c> tek
/// satır okuyarak son numarayı verir (tüm numaraları çekip bellekte maksimum aramaz).
/// </para>
/// <para>
/// <b>GoBD ile karıştırılmamalıdır:</b> kesintisiz (boşluksuz) sekans zorunluluğu
/// <b>faturalar</b> için geçerlidir (architecture.md §6.2 — <c>HotelInvoiceCounter</c> +
/// satır kilidi). Rezervasyon numarası bir <b>ticari referans</b>tır, muhasebe belgesi değil:
/// <list type="bullet">
///   <item>bu yüzden satır kilidi/sayaç tablosu kullanılmaz (rezervasyon oluşturma yolu
///         kilitlenmez, eşzamanlı check-in trafiği yavaşlamaz),</item>
///   <item>boşluk oluşabilir (iptal edilen numara yeniden kullanılmaz) — bu kabul edilebilir,</item>
///   <item>eşzamanlı iki isteğin aynı numarayı üretmesi hâlinde nihai güvence
///         <c>Reservation(HotelId, ReservationNumber)</c> <b>unique index</b>'idir; çağıran
///         (bkz. <c>CreateReservationHandler</c>) numarayı yenileyerek yeniden dener.</item>
/// </list>
/// </para>
/// <para>
/// <b>Otel kapsamı açıkça verilir:</b> Head Office kullanıcısında global query filter
/// bypass edildiği için <c>HotelId</c> koşulu yazılmasa numara başka otellerin
/// rezervasyonlarından türetilebilirdi.
/// </para>
/// </summary>
internal sealed class ReservationNumberGenerator(IAppDbContext database, IDateTimeProvider clock)
{
    private const string Prefix = "RES";

    private const int SequenceDigits = 5;

    /// <summary>Aynı numara çarpışmasında yeniden deneme sayısı (yarış durumu savunması).</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Otel için sıradaki rezervasyon numarasını üretir.
    /// </summary>
    /// <param name="hotelId">Aktif otel.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public async Task<string> NextAsync(Guid hotelId, CancellationToken cancellationToken)
    {
        var year = clock.UtcNow.UtcDateTime.Year;
        var prefix = $"{Prefix}-{year.ToString(CultureInfo.InvariantCulture)}-";

        // CA1310 bastırılır: StringComparison'lı aşırı yüklemeyi EF Core SQL'e çeviremez.
        // Parametresiz StartsWith burada .NET'te değil PostgreSQL'de `LIKE 'RES-2026-%'` olur
        // (index kullanılabilir); karşılaştırma veritabanı collation'ına göre yapılır.
#pragma warning disable CA1310
        var lastNumber = await database.Reservations
            .Where(reservation => reservation.HotelId == hotelId
                                  && reservation.ReservationNumber.StartsWith(prefix))
#pragma warning restore CA1310
            .OrderByDescending(reservation => reservation.ReservationNumber)
            .Select(reservation => reservation.ReservationNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var next = ParseSequence(lastNumber, prefix) + 1;

        return prefix + next.ToString(CultureInfo.InvariantCulture).PadLeft(SequenceDigits, '0');
    }

    /// <summary>
    /// Numaranın sıra kısmını çözer. Beklenmeyen bir biçimde (elle girilmiş kayıt) 0 döner:
    /// üretim durmaz, en kötü hâlde unique index çarpışması olur ve çağıran yeniden dener.
    /// </summary>
    private static int ParseSequence(string? reservationNumber, string prefix)
    {
        if (string.IsNullOrEmpty(reservationNumber) || !reservationNumber.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        var sequencePart = reservationNumber[prefix.Length..];

        return int.TryParse(sequencePart, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
