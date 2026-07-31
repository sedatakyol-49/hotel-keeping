using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// <c>AddPublicBookingChannel</c> migration'inin <b>on ucus (pre-flight)</b> denetiminin
/// kesisim mantigini dogrular.
///
/// <para><b>Neden ayri bir test:</b> migration gercek veritabaninda calisti ve <b>sifir</b>
/// cakisma buldu — yani SQL'in gecerli oldugu kanitlandi, ama <i>tespit ettigi</i> kanitlanmadi.
/// Denetim yanlissa hata kaybolmaz, yalnizca operatore okunur mesaj yerine ham bir
/// <c>23P01</c> gosterilir; yine de kural sessizce yanlis kalmamalidir.</para>
///
/// <para><b>Neden gercek satir yazilmiyor:</b> cakisan rezervasyonlari yazabilmek icin
/// <c>EX_Reservations_NoOverlappingStays</c> kisitinin gecici olarak dusurulmesi gerekirdi; bu,
/// paylasilan bir gelistirme veritabaninda tabloyu ACCESS EXCLUSIVE ile kilitler ve testin
/// yarida kesilmesi hâlinde kisiti kaldirilmis birakabilir. Bunun yerine <b>ayni kesisim
/// predikati</b> sentetik bir satir kumesi (VALUES) uzerinde kosturulur — dogrulanan sey zaten
/// predikatin kendisidir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OverlapPreflightQueryTests(PostgresFixture fixture)
{
    /// <summary>
    /// Migration'daki JOIN kosulunun birebir aynisi: yari acik aralik kesisimi + bloke eden
    /// durum kumesi + silinmemis satirlar.
    /// </summary>
    private static readonly System.Text.CompositeFormat DetectionQuery =
        System.Text.CompositeFormat.Parse(DetectionSql);

    private const string DetectionSql = """
        WITH sample("Id", "RoomId", "CheckIn", "CheckOut", "Status", "IsDeleted") AS (
            VALUES {0}
        )
        SELECT count(*)::int AS "Value"
        FROM sample a
        JOIN sample b
          ON a."RoomId" = b."RoomId"
         AND a."Id" < b."Id"
         AND a."CheckIn" < b."CheckOut"
         AND b."CheckIn" < a."CheckOut"
        WHERE a."Status" NOT IN ('Cancelled', 'NoShow')
          AND b."Status" NOT IN ('Cancelled', 'NoShow')
          AND NOT a."IsDeleted"
          AND NOT b."IsDeleted"
        """;

    [RequiresPostgresTheory]
    // Ayni oda, kesisen tarihler -> CAKISMA.
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false), "
                + "(2, 1, DATE '2026-08-12', DATE '2026-08-15', 'Confirmed', false)", 1)]
    // Ardisik konaklama (cikis gunu = giris gunu) -> cakisma DEGIL (yari acik aralik).
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false), "
                + "(2, 1, DATE '2026-08-13', DATE '2026-08-15', 'Confirmed', false)", 0)]
    // Farkli odalar -> cakisma DEGIL.
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false), "
                + "(2, 2, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false)", 0)]
    // Iptal edilmis satir kismi predikatin disindadir.
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-13', 'Cancelled', false), "
                + "(2, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false)", 0)]
    // Gelmeyen misafir de oyle.
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-13', 'NoShow', false), "
                + "(2, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false)", 0)]
    // Soft-delete edilmis satir da oyle.
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', true), "
                + "(2, 1, DATE '2026-08-10', DATE '2026-08-13', 'Confirmed', false)", 0)]
    // Tam kapsayan aralik -> CAKISMA.
    [InlineData("(1, 1, DATE '2026-08-10', DATE '2026-08-20', 'Confirmed', false), "
                + "(2, 1, DATE '2026-08-12', DATE '2026-08-14', 'Confirmed', false)", 1)]
    public async Task The_preflight_predicate_matches_the_constraint_semantics(string rows, int expected)
    {
        await fixture.EnsureMigratedAsync();
        await using var database = fixture.CreateDbContext();

        // Sabit, testte tanimli SQL parcasi; kullanici girdisi degildir.
        var sql = string.Format(System.Globalization.CultureInfo.InvariantCulture, DetectionQuery, rows);

        var conflicts = await database.Database.SqlQueryRaw<int>(sql).SingleAsync();

        conflicts.Should().Be(expected);
    }
}
