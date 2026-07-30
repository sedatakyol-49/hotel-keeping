using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Reservations;

/// <summary>
/// Folio konaklama satirinin <b>UTF-8 regresyon agi</b> — kardesi
/// <c>Settings/SettingsEncodingTests</c>'tir, ayni gerekceyle yazilmistir.
///
/// <para><b>Kilitlenen hata:</b> folio satirinin aciklamasi Almanca'dir (folio otel ici bir
/// defterdir) ama umlaut <b>ASCII'ye indirgenmisti</b>: <c>Ubernachtung</c>. Bu bir kodlama
/// duzeltmesi degil, bir <b>yazim hatasi</b>ydi — kolon ve JSON zaten UTF-8'dir. Terminalde
/// mojibake gorunce metni ASCII'lestirmek refleksi bu yuzden tehlikelidir: belirtiyi degil,
/// dogru metni yok eder.</para>
///
/// <para>Dogrulama iki katmanda: (1) HTTP yanitinin <b>ham baytlari</b> — <c>ü</c> icin
/// <c>0xC3 0xBC</c> dizisi bulunur, cift kodlamanin uretecegi <c>Ã</c> bulunmaz; (2) dogrudan
/// veritabani satiri — API katmani atlanarak saklanan metin karsilastirilir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FolioEncodingTests(PostgresFixture fixture)
{
    /// <summary>Konaklama satirinin Almanca basligi — umlaut KORUNUR.</summary>
    private const string RoomChargeLabel = "Übernachtung";

    /// <summary>Cift kodlanmis (mojibake) umlaut'un ilk karakteri.</summary>
    private const string MojibakeMarker = "Ã";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [RequiresPostgresFact]
    public async Task The_folio_room_charge_is_labelled_Ubernachtung_with_its_umlaut()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient([Permissions.ReservationsView]);

        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        using var response = await client.GetAsync(
            new Uri($"api/v1/reservations/{reservation.Id}/folio", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        // (1) Ham baytlar: gercekten UTF-8 mi, cift kodlanmis mi?
        var rawBytes = await response.Content.ReadAsByteArrayAsync();
        var decoded = Encoding.UTF8.GetString(rawBytes);

        decoded.Should().Contain(RoomChargeLabel);
        decoded.Should().NotContain(
            MojibakeMarker,
            "cift kodlama olsaydi 'ü' yerine 'Ã' + ikinci bir karakter gorunurdu");

        // 'Ü' (U+00DC) UTF-8'de 0xC3 0x9C dizisidir. Baytlara dogrudan bakmak kodlamayi
        // gorunur kilar: Latin-1/cp1252 kodlanmis bir govdede tek bir 0xDC bayti olurdu.
        ContainsSequence(rawBytes, [0xC3, 0x9C]).Should().BeTrue(
            "'Ü' govdede UTF-8 olarak (0xC3 0x9C) tasinmalidir");
        rawBytes.Should().NotContain(
            (byte)0xDC,
            "tek basina 0xDC Latin-1/cp1252 kodlamasinin izidir");

        var folio = JsonSerializer.Deserialize<FolioResponse>(decoded, WebJson);

        var roomCharge = folio!.Lines.Should()
            .ContainSingle(line => line.Type == nameof(InvoiceLineType.RoomCharge)).Subject;

        roomCharge.Description.Should().StartWith(
            RoomChargeLabel,
            "aciklama Almanca yazilir ve umlaut ASCII'ye indirgenmez");

        // (2) API katmani atlanarak dogrudan veritabani satiri.
        await using var database = fixture.CreateDbContext();
        var stored = await database.InvoiceLineItems.IgnoreQueryFilters().AsNoTracking()
            .Where(line => line.HotelId == scenario.HotelAId && line.Type == InvoiceLineType.RoomCharge)
            .Select(line => line.Description)
            .SingleAsync();

        stored.Should().StartWith(RoomChargeLabel);
    }

    /// <summary>Bayt dizisinde bir alt dizinin gecip gecmedigi (kodlama denetimi icin).</summary>
    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var index = 0; index + needle.Length <= haystack.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
