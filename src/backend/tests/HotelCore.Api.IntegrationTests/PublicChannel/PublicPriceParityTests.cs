using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.Invoices.Create;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// <b>Fiyat eşitliği</b> (architecture-public-booking.md §8): misafire gösterilen
/// <c>price.totalGross</c>, o rezervasyondan üretilen faturanın <c>grossAmount</c>'una
/// <b>kuruşu kuruşuna</b> eşittir.
///
/// <para><b>Bu testin amacı bir hesabı doğrulamak değil, ikinci bir fiyat motorunun sessizce
/// doğmasını kalıcı olarak engellemektir.</b> Public teklif <c>ReservationPricingService</c> +
/// <c>InvoiceAmounts</c> + <c>TaxProfile.CountTaxablePersons</c> kullanır; biri kopyalanırsa iki
/// taraf ilk yuvarlama farkında ayrışır ve bu test kırılır.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicPriceParityTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Public_offer_total_equals_the_generated_invoice_gross_amount()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        var checkOut = checkIn.AddDays(3);

        // 1) Misafirin gördüğü teklif.
        var availability = await scenario.GetAsync(
            $"/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&adults=2&children=0");

        availability.StatusCode.Should().Be(HttpStatusCode.OK);

        using var availabilityBody = await PublicChannelScenario.ReadJsonAsync(availability);
        var offer = availabilityBody!.RootElement.GetProperty("offers").EnumerateArray()
            .Single(item => item.GetProperty("roomTypeCode").GetString() == PublicChannelScenario.RoomTypeCodeA);

        var price = offer.GetProperty("price");
        var offeredTotal = price.GetProperty("totalGross").GetDecimal();
        var accommodationGross = price.GetProperty("accommodationGross").GetDecimal();
        var cityTax = price.GetProperty("cityTax").GetProperty("amount").GetDecimal();

        // PAngV değişmezleri.
        price.GetProperty("accommodationNet").GetDecimal()
            .Should().Be(accommodationGross - price.GetProperty("accommodationVat").GetDecimal());
        offeredTotal.Should().Be(accommodationGross + cityTax);
        price.GetProperty("nightly").EnumerateArray()
            .Sum(night => night.GetProperty("gross").GetDecimal())
            .Should().Be(accommodationGross);

        var taxablePersons = price.GetProperty("cityTax").GetProperty("taxablePersons").GetInt32();
        var nights = price.GetProperty("cityTax").GetProperty("nights").GetInt32();
        var perPersonNight = price.GetProperty("cityTax").GetProperty("perPersonNight").GetDecimal();
        cityTax.Should().Be(taxablePersons * nights * perPersonNight);

        // 2) Aynı teklifi hold + booking ile gerçek bir rezervasyona dönüştür.
        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkOut);
        var holdTotal = hold!.RootElement.GetProperty("price").GetProperty("totalGross").GetDecimal();
        holdTotal.Should().Be(offeredTotal, "hold, aramada gösterilen teklifi DONDURUR");

        var token = hold.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var bookingResponse = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));

        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var booking = await PublicChannelScenario.ReadJsonAsync(bookingResponse);
        var reference = booking!.RootElement.GetProperty("bookingReference").GetString()!;
        booking.RootElement.GetProperty("price").GetProperty("totalGross").GetDecimal()
            .Should().Be(offeredTotal, "rezervasyon yanıtı hold'da donmuş fiyatı taşır");

        // 3) Faturayı üret ve toplamı karşılaştır.
        var reservationId = await scenario.FindReservationIdAsync(reference);

        await using var graph = scenario.CreateApplicationGraph();
        var invoice = await graph.Dispatcher.Send(
            new CreateInvoiceRequest { ReservationId = reservationId });

        invoice.GrossAmount.Should().Be(
            offeredTotal,
            "misafire gösterilen toplam ile faturanın ürettiği tutar kuruşu kuruşuna uzlaşmalıdır");

        // Kırılım da uzlaşmalıdır: konaklama net+KDV, Kurtaxe ayrı toplam.
        invoice.NetAmount.Should().Be(price.GetProperty("accommodationNet").GetDecimal());
        invoice.VatAmount.Should().Be(price.GetProperty("accommodationVat").GetDecimal());
        invoice.CityTaxAmount.Should().Be(cityTax);
    }

    [RequiresPostgresFact]
    public async Task Season_change_is_priced_night_by_night_and_still_reconciles()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90);
        var checkOut = checkIn.AddDays(3);

        var availability = await scenario.GetAsync(
            $"/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&adults=2&children=0");

        using var body = await PublicChannelScenario.ReadJsonAsync(availability);
        var price = body!.RootElement.GetProperty("offers").EnumerateArray().First().GetProperty("price");

        var nightly = price.GetProperty("nightly").EnumerateArray().ToArray();

        // Gece gece kırılım verilmek ZORUNDADIR: PAngV, sezon geçişinde "gecelik X €" ifadesini
        // tek bir sayıya indirgemeyi yasaklar; ortalama AYRI ve etiketli bir alandır.
        nightly.Should().HaveCount(3);
        nightly.Sum(night => night.GetProperty("gross").GetDecimal())
            .Should().Be(price.GetProperty("accommodationGross").GetDecimal());
        price.GetProperty("averageNightlyGross").ValueKind.Should().Be(JsonValueKind.Number);
    }
}
