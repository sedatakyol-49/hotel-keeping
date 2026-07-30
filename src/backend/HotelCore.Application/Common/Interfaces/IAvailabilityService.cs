namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Müsaitlik motoru (architecture.md §4.3 — Odoo "availability engine" karşılığı):
/// bir odanın belirli tarih aralığında satılabilir olup olmadığını söyler ve çakışan
/// rezervasyonu engeller.
/// <para>
/// <b>Çakışma kuralı — YARI AÇIK ARALIK <c>[CheckIn, CheckOut)</c>:</b> giriş günü aralığa
/// DAHİL, çıkış günü DAHİL DEĞİLDİR. Bunun somut sonucu: bir misafirin çıkış yaptığı gün
/// (<c>CheckOut</c>) aynı odaya <b>aynı gün</b> giriş yapacak başka bir rezervasyonun
/// <c>CheckIn</c>'i olabilir — otelcilikte normal olan "sabah çıkış / öğleden sonra giriş"
/// akışı budur ve bu iki rezervasyon çakışmaz.
/// Kesişim testi bu yüzden <c>mevcut.CheckIn &lt; istenen.CheckOut &amp;&amp;
/// istenen.CheckIn &lt; mevcut.CheckOut</c> şeklindedir (uç noktalarda eşitlik çakışma DEĞİLDİR).
/// </para>
/// <para>
/// <b>Kapsam dışı bırakılanlar:</b> <c>Cancelled</c> ve <c>NoShow</c> rezervasyonlar çakışma
/// üretmez (oda tekrar satılabilir); <c>IsOutOfOrder</c> odalar hiçbir tarihte müsait sayılmaz.
/// </para>
/// <para>
/// Bu arayüz Application katmanında tanımlanır <b>ve</b> Application katmanında uygulanır:
/// müsaitlik bir iş kuralıdır, altyapı detayı değildir; veriye <see cref="IAppDbContext"/>
/// portu üzerinden erişilir. Tenant izolasyonu ve soft-delete global query filter'dan gelir.
/// </para>
/// </summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Odanın <paramref name="checkIn"/> – <paramref name="checkOut"/> aralığında (yarı açık)
    /// çakışan rezervasyonu olup olmadığını döner. Odanın servis dışı olması burada
    /// değerlendirilmez — yalnızca takvim çakışmasına bakar.
    /// </summary>
    /// <param name="roomId">Oda kimliği.</param>
    /// <param name="checkIn">Giriş günü (dahil).</param>
    /// <param name="checkOut">Çıkış günü (dahil değil).</param>
    /// <param name="excludeReservationId">
    /// Güncellenen rezervasyonun kendisi (kendisiyle çakıştığı için hariç tutulur).
    /// </param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task<bool> IsRoomFreeAsync(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? excludeReservationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Odanın satılabilir olduğunu garanti eder; aksi hâlde istisna fırlatır:
    /// oda yok/başka otelde ise <c>NotFoundException</c> (404), oda servis dışıysa veya
    /// tarih aralığı başka bir rezervasyonla kesişiyorsa <c>ConflictException</c> (409).
    /// </summary>
    /// <param name="roomId">Oda kimliği.</param>
    /// <param name="checkIn">Giriş günü (dahil).</param>
    /// <param name="checkOut">Çıkış günü (dahil değil).</param>
    /// <param name="excludeReservationId">Güncellenen rezervasyonun kendisi.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task EnsureRoomIsBookableAsync(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? excludeReservationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Aralık boyunca <b>tamamı</b> müsait olan odaların kimliklerini döner (servis dışı odalar
    /// ve çakışan rezervasyonu olanlar hariç). Yalnızca kimlik döner; oda ayrıntıları çağıran
    /// slice'ın izdüşümüne bırakılır (arayüz DTO'ya bağımlı olmaz).
    /// </summary>
    /// <param name="rangeStart">Aralığın ilk günü (dahil).</param>
    /// <param name="rangeEnd">
    /// Aralığın bitiş günü (dahil değil). Parametre adı bilinçli olarak <c>to</c> değil:
    /// arayüz üyelerinde dil anahtar kelimeleriyle çakışmamalıdır (CA1716).
    /// </param>
    /// <param name="roomTypeId">Opsiyonel oda tipi filtresi.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task<IReadOnlyList<Guid>> GetAvailableRoomIdsAsync(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        Guid? roomTypeId,
        CancellationToken cancellationToken);
}
