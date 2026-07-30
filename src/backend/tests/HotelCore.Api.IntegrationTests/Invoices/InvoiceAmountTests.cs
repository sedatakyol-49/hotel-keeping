using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Invoices.GetById;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Fatura tutar matematigi: KDV orani eslemesi, satir bazinda yuvarlama ve
/// <c>net + kdv == brut</c> degismezi.
/// <para>
/// Birim fiyatlar <b>brut</b>tur (KDV dahil) ve KDV satirdan <i>icinden cikarilir</i>; oranlar
/// istemciden ALINMAZ, otelin <c>TaxProfile</c>'indan cozulur (A oteli: %19 standart, %7
/// indirimli, Kurtaxe %0).
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceAmountTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Room_charges_use_the_reduced_rate_extras_the_standard_rate_and_city_tax_zero()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var invoice = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.RoomCharge, "Ubernachtung", 1m, 107m),
            BookingScenario.Line(InvoiceLineType.Extra, "Fruhstuck", 1m, 119m),
            BookingScenario.Line(InvoiceLineType.CityTax, "Kurtaxe", 4m, 3m));

        var room = invoice.LineItems.Single(line => line.Type == nameof(InvoiceLineType.RoomCharge));
        var extra = invoice.LineItems.Single(line => line.Type == nameof(InvoiceLineType.Extra));
        var cityTax = invoice.LineItems.Single(line => line.Type == nameof(InvoiceLineType.CityTax));

        room.VatRate.Should().Be(7m, "konaklama indirimli orandan vergilenir (UStG §12/2/11)");
        room.LineNet.Should().Be(100m);
        room.LineVat.Should().Be(7m);

        extra.VatRate.Should().Be(19m, "kahvalti/ekstra hizmetler standart orandadir");
        extra.LineNet.Should().Be(100m);
        extra.LineVat.Should().Be(19m);

        cityTax.VatRate.Should().Be(0m, "Kurtaxe durchlaufender Posten'dir; KDV matrahina girmez");
        cityTax.LineNet.Should().Be(12m);
        cityTax.LineVat.Should().Be(0m);
    }

    [RequiresPostgresFact]
    public async Task Invoice_totals_keep_city_tax_out_of_the_vat_base()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var invoice = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.RoomCharge, "Ubernachtung", 1m, 107m),
            BookingScenario.Line(InvoiceLineType.Extra, "Fruhstuck", 1m, 119m),
            BookingScenario.Line(InvoiceLineType.CityTax, "Kurtaxe", 4m, 3m));

        invoice.NetAmount.Should().Be(200m, "Kurtaxe NetAmount'a dahil DEGILDIR");
        invoice.VatAmount.Should().Be(26m);
        invoice.CityTaxAmount.Should().Be(12m);
        invoice.GrossAmount.Should().Be(238m);

        (invoice.NetAmount + invoice.VatAmount + invoice.CityTaxAmount)
            .Should().Be(invoice.GrossAmount);
    }

    [RequiresPostgresFact]
    public async Task Rounding_happens_per_line_so_net_plus_vat_equals_the_printed_line_amount()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        // 10,00 / 1,19 = 8,4033... -> net 8,40 ve KDV kalan olarak 1,60 (kurus kacagi olmaz).
        var invoice = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m),
            BookingScenario.Line(InvoiceLineType.Extra, "Parkplatz", 1m, 10m),
            BookingScenario.Line(InvoiceLineType.Extra, "Wasser", 1m, 10m));

        invoice.LineItems.Should().AllSatisfy(line =>
        {
            line.LineNet.Should().Be(8.40m);
            line.LineVat.Should().Be(1.60m);
            (line.LineNet + line.LineVat).Should().Be(line.LineGross);
        });

        // Toplam, YUVARLANMIS satirlarin toplamidir (toplami yeniden yuvarlamak kurus farki yaratirdi).
        invoice.NetAmount.Should().Be(25.20m);
        invoice.VatAmount.Should().Be(4.80m);
        invoice.GrossAmount.Should().Be(30m);
    }

    [RequiresPostgresFact]
    public async Task Quantity_multiplication_is_rounded_before_the_vat_split()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        // 3 x 33,33 = 99,99 (brut) -> net = round(99,99 / 1,19) = 84,03; KDV = 15,96.
        var invoice = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Wellness", 3m, 33.33m));

        var line = invoice.LineItems.Should().ContainSingle().Which;
        line.LineGross.Should().Be(99.99m);
        line.LineNet.Should().Be(84.03m);
        line.LineVat.Should().Be(15.96m);
        (line.LineNet + line.LineVat).Should().Be(99.99m);
    }

    [RequiresPostgresFact]
    public async Task The_cancellation_invoice_is_symmetric_down_to_the_cent()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        // Bilincli olarak "cirkin" tutarlar: yuvarlama simetrisi burada kirilirdi.
        var original = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.RoomCharge, "Ubernachtung", 3m, 33.33m),
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m),
            BookingScenario.Line(InvoiceLineType.CityTax, "Kurtaxe", 5m, 3m));

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });
        var storno = await scenario.Host.Dispatcher.Send(
            new GetInvoiceByIdRequest(afterCancel.CancelledByInvoiceId!.Value));

        (original.NetAmount + storno.NetAmount).Should().Be(0m);
        (original.VatAmount + storno.VatAmount).Should().Be(0m);
        (original.CityTaxAmount + storno.CityTaxAmount).Should().Be(0m);
        (original.GrossAmount + storno.GrossAmount).Should().Be(0m);

        // Negatif tarafta da net + kdv == brut degismezi korunur.
        storno.LineItems.Should().AllSatisfy(line =>
            (line.LineNet + line.LineVat).Should().Be(line.LineGross));
        (storno.NetAmount + storno.VatAmount + storno.CityTaxAmount)
            .Should().Be(storno.GrossAmount);
    }

    [Fact]
    public void The_line_input_contract_exposes_no_tax_or_total_fields()
    {
        // Istemci vergi matrahini manipule edememelidir: satir girdisinde vatRate/lineNet/lineVat
        // ALANI YOKTUR. Bu, sozlesmenin kendisiyle kilitlenir (yalnizca davranisla degil) ve
        // veritabani gerektirmez.
        typeof(InvoiceLineInput)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
            [
                nameof(InvoiceLineInput.Type),
                nameof(InvoiceLineInput.Description),
                nameof(InvoiceLineInput.Quantity),
                nameof(InvoiceLineInput.UnitPrice),
                nameof(InvoiceLineInput.ServiceDate)
            ]);
    }
}
