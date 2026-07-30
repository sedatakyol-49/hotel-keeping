using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda sorgularının paylaşılan parçaları: doğal (numerik) sıralama ve izdüşümler.
/// Hepsi <b>sunucu tarafında</b> (SQL) çalışır; hiçbir yerde tüm kayıtlar çekilip bellekte
/// sıralanmaz.
/// </summary>
internal static class RoomQueryExtensions
{
    /// <summary>
    /// Sözleşmedeki sıralama: <c>floor</c>, sonra <c>number</c> <b>doğal/numerik</b> sırada
    /// ("2" &lt; "10", "201" &lt; "1001").
    /// <para>
    /// <b>PostgreSQL'de nasıl çözülüyor:</b> <c>Number</c> bir metin kolonu olduğu için düz
    /// <c>ORDER BY number</c> sözlük sırası verir ve "10" &lt; "2" olur. Bunun yerine
    /// <c>ORDER BY floor, length(number), number</c> üretiliyor (EF Core <c>string.Length</c>
    /// çağrısını Npgsql üzerinde <c>length(...)</c>'a çevirir):
    /// <list type="number">
    ///   <item>önce uzunluk → basamak sayısı ("2" tek basamak, "10" iki basamak, "1001" dört),</item>
    ///   <item>aynı uzunlukta ise sözlük sırası = numerik sıra (sabit basamaklı sayılarda eşdeğer).</item>
    /// </list>
    /// Bu yaklaşım <c>"201A"</c>, <c>"P1"</c> gibi <b>alfanümerik</b> oda numaralarında da
    /// kararlı ve deterministiktir. Alternatif olan <c>ORDER BY (regexp_replace(number,'\D','','g'))::int</c>
    /// hem ham SQL/veritabanına özgü fonksiyon gerektirir hem de rakam içermeyen bir numarada
    /// çalışma zamanı hatası (invalid input syntax for integer) verir; bu yüzden tercih edilmedi.
    /// Son <c>ThenBy(Id)</c> sayfalamanın (Skip/Take) eşitlik durumunda kararlı kalmasını sağlar.
    /// </para>
    /// </summary>
    public static IOrderedQueryable<Room> OrderByFloorThenNumber(this IQueryable<Room> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query
            .OrderBy(room => room.Floor)
            .ThenBy(room => room.Number.Length)
            .ThenBy(room => room.Number)
            .ThenBy(room => room.Id);
    }

    /// <summary>
    /// Oda listesi/detayı izdüşümü. Oda tipi bilgisi JOIN ile alınır (Include yerine izdüşüm:
    /// yalnızca iki kolon okunur). Oda tipi satırı görünmezse (teorik: soft-delete edilmiş tip)
    /// boş metne düşülür.
    /// </summary>
    public static IQueryable<RoomRow> ProjectToRow(this IQueryable<Room> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(room => new RoomRow(
            room.Id,
            room.Number,
            room.Floor,
            room.RoomTypeId,
            room.RoomType.Code ?? string.Empty,
            room.RoomType.Name ?? string.Empty,
            room.HousekeepingStatus,
            room.IsOutOfOrder,
            room.Note));
    }

    /// <summary>
    /// Pano izdüşümü — <b>fiyat kolonları seçilmez</b> (RBAC §7: Housekeeping rolü finansal
    /// veri görmez; veri hiç okunmadığı için yanıta sızma olasılığı da yoktur).
    /// </summary>
    public static IQueryable<RoomBoardRow> ProjectToBoardRow(this IQueryable<Room> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(room => new RoomBoardRow(
            room.Id,
            room.Number,
            room.Floor,
            room.RoomType.Code ?? string.Empty,
            room.HousekeepingStatus,
            room.IsOutOfOrder,
            room.Note));
    }
}
