using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.AddPayment;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.GetById;
using HotelCore.Application.Features.Invoices.List;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// GoBD §6.1 — <b>Stornorechnung</b> (iptal faturasi) davranisi.
/// <para>
/// Kilitlenen iddialar: orijinal belge <b>aynen korunur</b> (tutar ve numara degismez), iptal
/// faturasi tutarlari <b>negatiftir</b> ve orijinali tam olarak sifirlar, iki yonlu bag
/// (<c>cancelledByInvoiceId</c> ↔ <c>cancelsInvoiceId</c>) kurulur; taslak iptali ise storno
/// URETMEZ ve numara tuketmez.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceCancellationTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Cancelling_a_finalized_invoice_preserves_the_original_document()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 2m, 12m));

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest
        {
            Id = original.Id,
            Reason = "Yanlis misafire kesildi."
        });

        afterCancel.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        afterCancel.InvoiceNumber.Should().Be(original.InvoiceNumber, "belge numarasi silinmez");
        afterCancel.GrossAmount.Should().Be(original.GrossAmount, "orijinalin tutari degistirilmez");
        afterCancel.IssuedAt.Should().Be(original.IssuedAt);
        afterCancel.CancelledByInvoiceId.Should().NotBeNull();
        afterCancel.IsCancellationInvoice.Should().BeFalse();
    }

    [RequiresPostgresFact]
    public async Task The_cancellation_invoice_mirrors_the_original_with_negative_amounts()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.RoomCharge, "Ubernachtung", 2m, 120m),
            BookingScenario.Line(InvoiceLineType.Extra, "Fruhstuck", 2m, 18m));

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });
        var storno = await scenario.Host.Dispatcher.Send(
            new GetInvoiceByIdRequest(afterCancel.CancelledByInvoiceId!.Value));

        storno.Status.Should().Be(nameof(InvoiceStatus.Finalized));
        storno.InvoiceNumber.Should().Be(scenario.InvoiceNumber(2), "storno da numarali bir belgedir");
        storno.NetAmount.Should().Be(-original.NetAmount);
        storno.VatAmount.Should().Be(-original.VatAmount);
        storno.CityTaxAmount.Should().Be(-original.CityTaxAmount);
        storno.GrossAmount.Should().Be(-original.GrossAmount);

        // Orijinal + storno = 0 (kurusu kurusuna): satirlar yeniden hesaplanmaz, negatiflenir.
        (original.GrossAmount + storno.GrossAmount).Should().Be(0m);
        (original.NetAmount + storno.NetAmount).Should().Be(0m);
        (original.VatAmount + storno.VatAmount).Should().Be(0m);

        storno.LineItems.Should().HaveCount(original.LineItems.Count);
        storno.LineItems.Should().AllSatisfy(line => line.Description.Should().StartWith("Storno: "));
        storno.LineItems.Zip(original.LineItems).Should().AllSatisfy(pair =>
        {
            pair.First.Quantity.Should().Be(pair.Second.Quantity);
            pair.First.VatRate.Should().Be(pair.Second.VatRate);
            pair.First.LineNet.Should().Be(-pair.Second.LineNet);
            pair.First.LineVat.Should().Be(-pair.Second.LineVat);
        });
    }

    [RequiresPostgresFact]
    public async Task The_storno_pair_is_linked_in_both_directions()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await scenario.CreateFinalizedInvoiceAsync();

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });
        var stornoId = afterCancel.CancelledByInvoiceId!.Value;
        var storno = await scenario.Host.Dispatcher.Send(new GetInvoiceByIdRequest(stornoId));

        // Ileri yon: orijinal -> storno.
        afterCancel.CancelledByInvoiceId.Should().Be(stornoId);
        // Geri yon: storno -> orijinal (ilintili alt sorgu olmadan okunabilsin diye saklanir).
        storno.CancelsInvoiceId.Should().Be(original.Id);
        storno.IsCancellationInvoice.Should().BeTrue();

        // Ham satirlarda da her iki kolon dolu olmali (yanit hesaplanmis bir alan degil).
        (await scenario.FindInvoiceAsync(original.Id))!.CancelledByInvoiceId.Should().Be(stornoId);
        (await scenario.FindInvoiceAsync(stornoId))!.CancelsInvoiceId.Should().Be(original.Id);
    }

    [RequiresPostgresFact]
    public async Task A_paid_invoice_can_also_be_cancelled_with_a_storno()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await scenario.CreateFinalizedInvoiceAsync();
        await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = original.Id,
            Amount = original.GrossAmount,
            Method = PaymentMethod.Cash
        });

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });

        afterCancel.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        afterCancel.CancelledByInvoiceId.Should().NotBeNull();
        afterCancel.PaidAmount.Should().Be(original.GrossAmount, "tahsilat kaydi silinmez");
    }

    [RequiresPostgresFact]
    public async Task Cancelling_a_draft_creates_no_storno_and_consumes_no_number()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        var cancelled = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = draft.Id });

        cancelled.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        cancelled.CancelledByInvoiceId.Should().BeNull("taslak bir belge degildir; storno gerekmez");
        (await scenario.FindInvoiceCounterAsync()).Should().BeNull();

        var page = await scenario.Host.Dispatcher.Send(new ListInvoicesRequest());
        page.Items.Should().ContainSingle("iptal faturasi olusturulmamali");
    }

    [RequiresPostgresFact]
    public async Task Cancelling_an_already_cancelled_invoice_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await scenario.CreateFinalizedInvoiceAsync();
        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });

        var act = async () => await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [RequiresPostgresFact]
    public async Task Cancelling_a_reservation_invoice_releases_folio_lines_back_to_the_folio()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var draft = await scenario.CreateReservationInvoiceAsync(reservation.Id);
        draft.LineItems.Should().Contain(line => line.Type == nameof(InvoiceLineType.RoomCharge));

        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = draft.Id });

        // Folio masrafi kaybolmaz: satir folio'da kalir ve yeniden faturalanabilir.
        var releasedLines = await scenario.Host.Database.InvoiceLineItems
            .Where(line => line.FolioId != null && line.InvoiceId == null)
            .CountAsync();
        releasedLines.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// <b>En pahali regresyon: kalici gelir kaybi.</b> Kesinlesmis fatura iptal edildiginde
    /// GoBD geregi bir Stornorechnung uretilir; iptal faturasi orijinalin <c>ReservationId</c>'sini
    /// tasir ve kendisi de <c>Finalized</c>'dir. "Rezervasyonun iptal edilmemis faturasi var mi?"
    /// diye soran eski kosul bu yuzden storno'yu <i>acik fatura</i> saniyordu: rezervasyon
    /// <b>kalici olarak</b> faturalanamaz hâle geliyor, ustelik hatanin kendi metni "duzeltmek
    /// icin storno olusturun" diyordu.
    /// <para>
    /// Yeni fatura, konaklama satiri orijinal belgede kilitli kaldigi icin (kesinlesmis faturanin
    /// satiri koparilamaz) <c>InvoiceLineComposer</c>'in geri dusus yolundan uretilir; bu yolun
    /// fiyat kaynagi <c>Reservation.TotalAmount</c>'tir. Bu yuzden yeni faturanin tutari
    /// orijinalinkine <b>kurusu kurusuna</b> esit olmalidir — aksi hâlde iptal-yeniden kesme
    /// dongusu her turda para uydurur ya da kaybederdi.
    /// </para>
    /// </summary>
    [RequiresPostgresFact]
    public async Task Cancelling_a_finalized_invoice_allows_the_reservation_to_be_invoiced_again()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var draft = await scenario.CreateReservationInvoiceAsync(reservation.Id);
        var original = await scenario.FinalizeInvoiceAsync(draft.Id);
        original.InvoiceNumber.Should().Be(scenario.InvoiceNumber(1));

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest
        {
            Id = original.Id,
            Reason = "Yanlis misafire kesildi."
        });

        afterCancel.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        afterCancel.CancelledByInvoiceId.Should().NotBeNull("kesinlesmis belge yalnizca storno ile iptal edilir");

        // ASIL IDDIA: storno kesildikten sonra rezervasyon yeniden faturalanabilir.
        var reissued = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        reissued.Id.Should().NotBe(original.Id);
        reissued.ReservationId.Should().Be(reservation.Id);
        reissued.Status.Should().Be(nameof(InvoiceStatus.Draft));
        reissued.GrossAmount.Should().Be(
            original.GrossAmount,
            "yeniden kesilen fatura ayni konaklamayi ayni tutarla belgelemelidir");

        var roomCharges = reissued.LineItems
            .Where(line => line.Type == nameof(InvoiceLineType.RoomCharge))
            .ToList();

        roomCharges.Should().ContainSingle("oda ucreti yeni faturada da tam olarak bir kez yer alir");
        roomCharges[0].LineGross.Should().Be(reservation.TotalAmount);
        reissued.CityTaxAmount.Should().Be(original.CityTaxAmount, "Kurtaxe yeniden uretilir");

        // Storno zinciri bozulmadi: orijinal hâlâ iptal, yeni fatura bagimsiz bir taslak.
        (await scenario.FindInvoiceAsync(original.Id))!.Status.Should().Be(InvoiceStatus.Cancelled);
        (await scenario.FindInvoiceAsync(reissued.Id))!.CancelsInvoiceId.Should().BeNull();
    }

    /// <summary>
    /// Storno istisnasinin <b>fazla genis kacmadigini</b> dogrular. Yururlukteki fatura tanimi
    /// (<c>InvoiceEffectiveness.IsEffective</c>) taslaklari <b>icerir</b>: taslak henuz belge
    /// olmasa da rezervasyon uzerinde acik bir faturadir ve ikincisi kesilmemelidir. Kesinlesmis
    /// ama iptal EDILMEMIS fatura da elbette engeller. Bu test olmadan 409 guard'i sessizce
    /// gevseyip mukerrer belge uretebilirdi.
    /// </summary>
    [RequiresPostgresFact]
    public async Task An_open_draft_still_blocks_a_second_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var draft = await scenario.CreateReservationInvoiceAsync(reservation.Id);
        draft.InvoiceNumber.Should().BeNull("taslak numara almaz; yine de acik bir faturadir");

        var whileDraft = async () => await scenario.CreateReservationInvoiceAsync(reservation.Id);
        await whileDraft.Should().ThrowAsync<ConflictException>();

        await scenario.FinalizeInvoiceAsync(draft.Id);

        var whileFinalized = async () => await scenario.CreateReservationInvoiceAsync(reservation.Id);
        await whileFinalized.Should().ThrowAsync<ConflictException>();

        // Rezervasyonun tek bir faturasi olusmus olmali: guard hicbir turda delinmedi.
        var invoiceCount = await scenario.Host.Database.Invoices
            .Where(invoice => invoice.ReservationId == reservation.Id)
            .CountAsync();

        invoiceCount.Should().Be(1);
    }

    [RequiresPostgresFact]
    public async Task A_reservation_can_be_invoiced_again_after_the_first_invoice_is_cancelled()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var first = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        // Ikinci fatura, ilki hala acikken reddedilir (mukerrer belge olusmasin).
        var duplicate = async () => await scenario.CreateReservationInvoiceAsync(reservation.Id);
        await duplicate.Should().ThrowAsync<ConflictException>();

        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = first.Id });

        var second = await scenario.CreateReservationInvoiceAsync(reservation.Id);
        second.Id.Should().NotBe(first.Id);
        second.ReservationId.Should().Be(reservation.Id);
    }
}
