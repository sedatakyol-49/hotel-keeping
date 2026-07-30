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
    /// <b>BILINEN HATA (raporlandi, uygulama kodu DEGISTIRILMEDI).</b>
    /// <para>
    /// Rezervasyondan uretilen faturada oda ucreti <b>iki kez</b> yer aliyor:
    /// <list type="number">
    ///   <item><c>ReservationFolioService.SyncRoomChargeAsync</c> rezervasyon olusturulurken
    ///         folio'ya bir <c>RoomCharge</c> satiri yazar,</item>
    ///   <item><c>InvoiceLineComposer.BuildFromReservationAsync</c> ise hem rezervasyondan
    ///         <b>yeni</b> bir <c>RoomCharge</c> satiri uretir hem de folio'nun faturalanmamis
    ///         tum satirlarini (yani ayni oda ucretini) faturaya tasir.</item>
    /// </list>
    /// Sonuc: 2 gece x 120,00 = 240,00 olmasi gereken konaklama 480,00 faturalaniyor.
    /// </para>
    /// <para>
    /// Test bilincli olarak <b>mevcut davranisi sabitler</b> (yesil kalsin diye beklenti
    /// zayiflatilmadi; dogru deger yorumda acikca yazili). Hata duzeltildiginde bu test
    /// KIRILACAKTIR ve tek yapilmasi gereken beklentiyi <c>reservation.TotalAmount</c>'a
    /// cekmektir.
    /// </para>
    /// </summary>
    [RequiresPostgresFact]
    public async Task The_room_charge_is_currently_billed_twice_known_defect()
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

        roomCharges.Should().HaveCount(2, "biri folio'dan tasiniyor, digeri rezervasyondan yeniden uretiliyor");
        roomCharges.Sum(line => line.LineGross)
            .Should().Be(480m, "DOGRUSU 240,00 olmaliydi — bkz. sinif belgesindeki bilinen hata");

        // Kurtaxe dogru: 2 yetiskin x 2 gece x 3,00 = 12,00.
        invoice.CityTaxAmount.Should().Be(12m);
        invoice.GrossAmount.Should().Be(492m, "480,00 (cift oda ucreti) + 12,00 Kurtaxe");
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
