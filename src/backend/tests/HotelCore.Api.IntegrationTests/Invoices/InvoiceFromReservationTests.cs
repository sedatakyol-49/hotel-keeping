using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.Create;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Rezervasyondan uretilen fatura: oda ucreti, folio ekstralari ve Kurtaxe sunucuda kurulur;
/// istemci satir gonderemez.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceFromReservationTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task The_invoice_inherits_the_guest_and_the_reservation_link()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var invoice = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        invoice.ReservationId.Should().Be(reservation.Id);
        invoice.ReservationNumber.Should().Be(reservation.ReservationNumber);
        invoice.GuestId.Should().Be(scenario.GuestAId);
        invoice.Currency.Should().Be("EUR", "para birimi otelden gelir, istemciden alinmaz");
    }

    [RequiresPostgresFact]
    public async Task Sending_manual_lines_together_with_a_reservation_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var act = async () => await scenario.Host.Dispatcher.Send(new CreateInvoiceRequest
        {
            ReservationId = reservation.Id,
            LineItems = [BookingScenario.Line(InvoiceLineType.Extra, "Elle eklendi", 1m, 500m)]
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [RequiresPostgresFact]
    public async Task An_invoice_without_a_reservation_and_without_lines_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var act = async () => await scenario.Host.Dispatcher.Send(new CreateInvoiceRequest
        {
            GuestId = scenario.GuestAId
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// Oda ucretinin <b>tek kaynagi folio'dur</b>: rezervasyon modulu satiri yazar ve tarih/oda
    /// degisiminde gunceller, fatura uretimi onu <b>tasir</b> — yeniden hesaplamaz.
    /// <para>
    /// Bu test bir regresyonu kilitler: fatura uretimi bir donem hem folio satirini tasiyor hem
    /// rezervasyondan ikinci bir <c>RoomCharge</c> uretiyordu ve 2 gece x 120,00 konaklama
    /// 480,00 faturalaniyordu.
    /// </para>
    /// </summary>
    [RequiresPostgresFact]
    public async Task The_room_charge_is_billed_exactly_once_and_comes_from_the_folio()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var invoice = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        var roomCharges = invoice.LineItems
            .Where(line => line.Type == nameof(InvoiceLineType.RoomCharge))
            .ToList();

        reservation.TotalAmount.Should().Be(240m, "2 gece x 120,00 dogru konaklama tutaridir");

        roomCharges.Should().ContainSingle("oda ucreti faturada tam olarak bir kez yer alir");
        roomCharges[0].LineGross.Should().Be(240m, "tutar reservation.TotalAmount ile birebir esittir");
        roomCharges[0].LineNet.Should().Be(224.30m);
        roomCharges[0].LineVat.Should().Be(15.70m);
        roomCharges[0].VatRate.Should().Be(7.00m, "konaklama indirimli KDV oranina tabidir");
        roomCharges[0].Quantity.Should().Be(2m);

        // Kurtaxe dogru: 2 yetiskin x 2 gece x 3,00 = 12,00.
        invoice.CityTaxAmount.Should().Be(12m);
        invoice.GrossAmount.Should().Be(252m, "240,00 konaklama + 12,00 Kurtaxe");

        // Satirin gercekten folio'dan geldigini kilitle: yeniden uretilseydi FolioId bos olurdu.
        var folioId = await scenario.Host.Database.InvoiceLineItems
            .Where(line => line.InvoiceId == invoice.Id && line.Type == InvoiceLineType.RoomCharge)
            .Select(line => line.FolioId)
            .FirstAsync();

        folioId.Should().NotBeNull("oda ucreti folio'dan tasinir, faturada yeniden uretilmez");
    }

    [RequiresPostgresFact]
    public async Task Folio_extras_are_moved_onto_the_invoice_and_cannot_be_billed_twice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var invoice = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        // Faturaya tasinan satirlar FolioId'lerini korur (masrafin kaynagi izlenebilir kalir),
        // ama artik InvoiceId dolu oldugu icin ikinci bir faturaya tasinamazlar.
        var carried = await scenario.Host.Database.InvoiceLineItems
            .Where(line => line.InvoiceId == invoice.Id && line.FolioId != null)
            .CountAsync();

        carried.Should().Be(1, "folio'nun konaklama satiri faturaya baglandi");

        var stillOpen = await scenario.Host.Database.InvoiceLineItems
            .Where(line => line.FolioId != null && line.InvoiceId == null)
            .CountAsync();

        stillOpen.Should().Be(0, "faturalanan masraf folio'da acik kalmaz");
    }
}
