using System.Globalization;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// §312j Abs. 2 BGB zorunlu özetindeki <b>kalem etiketleri</b> isteğin dilinde üretilir ve o
/// dilde <b>dondurulur</b>.
///
/// <para><b>Neden bu bir dil tercihi değil, hukuki bir gereklilik:</b> düğmenin hemen üstündeki
/// özet, sözleşmenin kurulduğu dilde "açık ve anlaşılır" olmak zorundadır. Ayrıca aynı metin
/// <c>PublicBooking.OrderSummaryJson</c>'a kanıt olarak yazılır ve admin tarafındaki
/// <c>/reservations/{id}/public-booking</c> ucundan okunur — uyuşmazlıkta otelin elindeki belge
/// budur. Sabit İngilizce bir etiket, <b>gösterilen</b> ile <b>saklanan</b> metni ayrıştırır ve
/// kanıtı değersizleştirir. (Uçtan uca doğrulamada gerçekten böyleydi: Almanca akışta özet
/// "City tax · 2 × 3 night(s)" yazıyordu.)</para>
///
/// <para><b>Hash neden bozulmuyor:</b> özet hold satırına dondurulur; <c>GET /holds/{token}</c>
/// yeniden hesaplamaz, JSON'u okur. Dolayısıyla misafir dili değiştirse bile hash aynı kalır ve
/// <c>409 SUMMARY_CHANGED</c> doğmaz.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicOrderSummaryLocalizationTests(PostgresFixture fixture)
{
    [RequiresPostgresTheory]
    [InlineData("de", "Übernachtung", "Kurtaxe", "Nächte", "Personen")]
    [InlineData("en", "Accommodation", "City tax", "nights", "guests")]
    [InlineData("tr", "Konaklama", "Şehir vergisi", "gece", "kişi")]
    public async Task Order_summary_component_labels_are_written_in_the_requested_language(
        string culture,
        string accommodationWord,
        string cityTaxWord,
        string nightWord,
        string personWord)
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);

        using var hold = await CreateHoldAsync(scenario, culture, checkIn);

        var components = hold.RootElement.GetProperty("orderSummary").GetProperty("components");
        var accommodation = Component(components, "Accommodation");
        var cityTax = Component(components, "CityTax");

        accommodation.Should().StartWith(accommodationWord);
        accommodation.Should().Contain(nightWord);

        cityTax.Should().StartWith(cityTaxWord);
        cityTax.Should().Contain(personWord);
        cityTax.Should().Contain(nightWord);
    }

    /// <summary>
    /// Tekil/çoğul biçim dilin kuralına göre seçilir: tek gecelik konaklamada Almanca "1 Nacht"
    /// yazılır. "1 Nächte" yazan bir özet, dikkatli okunması beklenen bir metin için kabul
    /// edilemez.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Single_night_uses_the_singular_form_in_german()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(41);

        using var hold = await CreateHoldAsync(scenario, "de", checkIn, nights: 1, adults: 1);

        var components = hold.RootElement.GetProperty("orderSummary").GetProperty("components");

        Component(components, "Accommodation").Should().Contain("1 Nacht").And.NotContain("Nächte");
        Component(components, "CityTax").Should().Contain("1 Person").And.NotContain("Personen");
    }

    /// <summary>
    /// Donmuş özet dile göre yeniden üretilmez: hold başka bir dille okunduğunda etiketler de
    /// hash de oluşturma anındaki hâlini korur.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Frozen_summary_does_not_change_when_the_hold_is_read_in_another_language()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(42);

        using var created = await CreateHoldAsync(scenario, "de", checkIn);
        var token = created.RootElement.GetProperty("holdToken").GetString()!;
        var createdSummary = created.RootElement.GetProperty("orderSummary");
        var createdHash = createdSummary.GetProperty("hash").GetString();
        var createdLabel = Component(createdSummary.GetProperty("components"), "CityTax");

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(scenario.Path($"/holds/{token}"), UriKind.Relative));
        request.Headers.Add("Accept-Language", "en");

        var response = await scenario.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var read = await PublicChannelScenario.ReadJsonAsync(response);
        var readSummary = read!.RootElement.GetProperty("orderSummary");

        readSummary.GetProperty("hash").GetString().Should().Be(createdHash);
        Component(readSummary.GetProperty("components"), "CityTax").Should().Be(createdLabel);
    }

    private static async Task<JsonDocument> CreateHoldAsync(
        PublicChannelScenario scenario,
        string culture,
        DateOnly checkIn,
        int nights = 3,
        int adults = 2)
    {
        var payload = JsonSerializer.Serialize(new
        {
            roomTypeCode = PublicChannelScenario.RoomTypeCodeA,
            checkIn = checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            checkOut = checkIn.AddDays(nights).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            adults,
            children = 0
        });

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(scenario.Path("/holds"), UriKind.Relative))
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Accept-Language", culture);

        var response = await scenario.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Content.Headers.ContentLanguage.Should().Contain(culture);

        return (await PublicChannelScenario.ReadJsonAsync(response))!;
    }

    private static string Component(JsonElement components, string kind) =>
        components.EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == kind)
            .GetProperty("label")
            .GetString()!;
}
