using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>Sunucu içi oda adayı — <b>hiçbir alanı yanıta yazılmaz</b> (oda no/kat yasak listede).</summary>
internal sealed record PublicRoomCandidate(Guid RoomId, Guid RoomTypeId, int Floor, string Number);

/// <summary>
/// Public müsaitlik: "bir oda tipi verilen aralıkta müsaittir ⇔ o tipte, <b>tüm gecelerde</b> boş
/// <b>ve</b> aktif hold'u olmayan en az bir oda vardır".
///
/// <para><b>Boşluk tanımı kopyalanmaz:</b> bloke eden rezervasyon kümesi mevcut
/// <see cref="AvailabilityQuery.BlockingBetween"/> ile <b>aynıdır</b> (yarı açık aralık
/// <c>[checkIn, checkOut)</c>; <c>Cancelled</c>/<c>NoShow</c> bloke etmez). Servis dışı oda
/// (<c>IsOutOfOrder</c>) müsait sayılmaz.</para>
///
/// <para><b>Hold'un yalnızca tavsiye niteliğinde olduğu nokta:</b> iki <c>EXCLUDE</c> kısıtı
/// <i>farklı tablolardadır</i> — resepsiyonun elle oluşturduğu bir rezervasyon aktif bir hold'la
/// çakışabilir ve veritabanı bunu engellemez. Bu yüzden nihai çakışmayı <c>Reservations</c>
/// kısıtı yakalar; misafire gösterilen sonuç <b>409 <c>ROOM_NO_LONGER_AVAILABLE</c></b>'dır ve
/// aynı hata metni her iki yolda da kullanılır: misafirin bakış açısından "oda kalmadı" tek bir
/// olaydır, iç mekanizma farkı ona ait değildir.</para>
/// </summary>
internal sealed class PublicAvailabilityReader(IAppDbContext database)
{
    /// <summary>
    /// Müsait oda sayısının misafire gösterilen üst sınırı. Ham sayı doluluğu ifşa ederdi;
    /// kırpma doğruluğu bozmaz (UWG §5) — gösterilen değer gerçek bir <b>alt sınırdır</b>.
    /// </summary>
    public const int AvailableUnitsCap = 5;

    /// <summary>
    /// Verilen aralıkta müsait odaları döner. Sıralama <b>deterministiktir</b>: <c>floor</c> ↑,
    /// sonra <c>number</c> ↑ (doğal sıra yaklaşımı: önce uzunluk, sonra sözlük sırası — böylece
    /// <c>"9"</c> &lt; <c>"10"</c>). Rastgele seçim testi imkânsızlaştırırdı.
    /// </summary>
    /// <param name="checkIn">Giriş günü (dahil).</param>
    /// <param name="checkOut">Çıkış günü (dahil değil).</param>
    /// <param name="now">Aktif hold'ların değerlendirileceği an.</param>
    /// <param name="roomTypeId">Tek bir oda tipiyle sınırlamak için; <c>null</c> = tüm tipler.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public async Task<IReadOnlyList<PublicRoomCandidate>> GetAvailableRoomsAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        DateTimeOffset now,
        Guid? roomTypeId,
        CancellationToken cancellationToken)
    {
        // Bloke eden rezervasyonlar: tanım TEK yerdedir (AvailabilityQuery), burada tekrarlanmaz.
        var blocked = database.Reservations.BlockingBetween(checkIn, checkOut);

        // Aktif hold: tüketilmemiş VE süresi dolmamış. Süresi dolmuş satır tabloda kalabilir
        // (fiziksel silme süpürücünün işidir), bu yüzden zaman koşulu sorguda uygulanır.
        var held = database.BookingHolds
            .Where(hold => hold.ConsumedAt == null
                           && hold.ExpiresAt > now
                           && hold.CheckIn < checkOut
                           && checkIn < hold.CheckOut);

        var rooms = database.Rooms.AsNoTracking().Where(room => !room.IsOutOfOrder);

        if (roomTypeId is Guid typeId)
        {
            rooms = rooms.Where(room => room.RoomTypeId == typeId);
        }

        return await rooms
            .Where(room => !blocked.Any(reservation => reservation.RoomId == room.Id))
            .Where(room => !held.Any(hold => hold.RoomId == room.Id))
            .OrderBy(room => room.Floor)
            .ThenBy(room => room.Number.Length)
            .ThenBy(room => room.Number)
            .Select(room => new PublicRoomCandidate(room.Id, room.RoomTypeId, room.Floor, room.Number))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sayıyı 5'te kırpar ve kırpılıp kırpılmadığını bildirir ("5+" gösterimi).</summary>
    public static (int Units, bool Capped) Cap(int available) =>
        available > AvailableUnitsCap ? (AvailableUnitsCap, true) : (available, false);
}
