using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Kurtaxe (sehir vergisi) hesabi ve <b>cocuk muafiyeti</b>.
/// <para>
/// Sahne her iki testte de aynidir — <b>2 yetiskin + 2 cocuk, 2 gece, kisi-gece basi 3,00 EUR</b> —
/// ve tek degisken otelin <c>cityTaxExemptChildren</c> ayaridir. Beklenen tutarlar birebir
/// kilitlenir: muafiyet KAPALI iken <b>24,00</b>, ACIK iken <b>12,00</b>.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceCityTaxTests(PostgresFixture fixture)
{
    private const int Adults = 2;
    private const int Children = 2;
    private const int Nights = 2;

    private static async Task<Guid> ArrangeStayAsync(BookingScenario scenario, bool exemptChildren)
    {
        await scenario.ConfigureCityTaxAsync(exemptChildren: exemptChildren);

        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(10 + Nights),
            adults: Adults,
            children: Children);

        return reservation.Id;
    }

    [RequiresPostgresFact]
    public async Task Without_the_child_exemption_every_guest_is_taxed()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservationId = await ArrangeStayAsync(scenario, exemptChildren: false);

        var invoice = await scenario.CreateReservationInvoiceAsync(reservationId);

        var cityTax = invoice.LineItems.Should()
            .ContainSingle(line => line.Type == nameof(InvoiceLineType.CityTax)).Which;

        // (2 yetiskin + 2 cocuk) x 2 gece = 8 kisi-gece.
        cityTax.Quantity.Should().Be(8m);
        cityTax.UnitPrice.Should().Be(BookingScenario.CityTaxPerPersonNight);
        cityTax.LineNet.Should().Be(24.00m);
        cityTax.LineVat.Should().Be(0m);
        invoice.CityTaxAmount.Should().Be(24.00m);
        cityTax.Description.Should().NotContain("exempt");
    }

    [RequiresPostgresFact]
    public async Task With_the_child_exemption_only_adults_are_taxed()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservationId = await ArrangeStayAsync(scenario, exemptChildren: true);

        var invoice = await scenario.CreateReservationInvoiceAsync(reservationId);

        var cityTax = invoice.LineItems.Should()
            .ContainSingle(line => line.Type == nameof(InvoiceLineType.CityTax)).Which;

        // Yalnizca 2 yetiskin x 2 gece = 4 kisi-gece → 24,00 yerine 12,00.
        cityTax.Quantity.Should().Be(4m);
        cityTax.LineNet.Should().Be(12.00m);
        invoice.CityTaxAmount.Should().Be(12.00m);

        // Muafiyetin dayanagi belgede gorunur (Kurtaxe beyani bu aciklamayi bekler).
        cityTax.Description.Should().Contain("children under 18 exempt");
    }

    [Fact]
    public void The_exemption_rule_lives_in_the_domain_tax_profile()
    {
        // Application katmani "adults + children" toplamini kendi basina YORUMLAMAZ; kuralin
        // sahibi domain metodudur. Kural burada dogrudan da kilitlenir (veritabani gerekmez).
        var profile = new TaxProfile { CityTaxExemptChildren = false };
        profile.CountTaxablePersons(Adults, Children).Should().Be(4);

        profile.CityTaxExemptChildren = true;
        profile.CountTaxablePersons(Adults, Children).Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task No_city_tax_line_is_produced_when_the_hotel_does_not_levy_it()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        await scenario.ConfigureCityTaxAsync(enabled: false);

        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(10 + Nights),
            adults: Adults,
            children: Children);

        var invoice = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        invoice.LineItems.Should().NotContain(line => line.Type == nameof(InvoiceLineType.CityTax));
        invoice.CityTaxAmount.Should().Be(0m);
    }
}
