using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.AddPayment;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Invoices.GetById;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Odeme kaydi: kismi odeme serbest, bakiye kapaninca <c>Paid</c>, <b>fazla odeme 409</b>,
/// taslaga/iptal edilmise odeme 409.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoicePaymentTests(PostgresFixture fixture)
{
    private const decimal Gross = 100m;

    private static AddInvoicePaymentRequest Payment(Guid invoiceId, decimal amount) => new()
    {
        InvoiceId = invoiceId,
        Amount = amount,
        Method = PaymentMethod.Card
    };

    private static Task<InvoiceDetailResponse> FinalizedAsync(BookingScenario scenario) =>
        scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, Gross));

    [RequiresPostgresFact]
    public async Task A_partial_payment_keeps_the_invoice_finalized_and_updates_the_balance()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);

        var afterPayment = await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 30m));

        afterPayment.Status.Should().Be(nameof(InvoiceStatus.Finalized));
        afterPayment.PaidAmount.Should().Be(30m);
        afterPayment.OutstandingAmount.Should().Be(70m);
        afterPayment.Payments.Should().ContainSingle();
    }

    [RequiresPostgresFact]
    public async Task Several_partial_payments_settle_the_invoice_and_move_it_to_paid()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);

        await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 30m));
        await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 60m));
        var settled = await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 10m));

        settled.Status.Should().Be(nameof(InvoiceStatus.Paid));
        settled.PaidAmount.Should().Be(Gross);
        settled.OutstandingAmount.Should().Be(0m);
        settled.Payments.Should().HaveCount(3);
    }

    [RequiresPostgresFact]
    public async Task An_overpayment_is_rejected_and_nothing_is_recorded()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);
        await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 90m));

        // Kalan 10,00; 10,01 bile reddedilir (kurus toleransi YOKTUR).
        var act = async () => await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 10.01m));

        await act.Should().ThrowAsync<ConflictException>();

        // Mesaj METNI dogrulanmaz: yerellestirildikten sonra (de/en/tr) aktif kulture bagli
        // olur ve CI makinesinin diline bagimlilik yaratirdi. Sozlesme acisindan onemli olan
        // sonuctur — istek reddedildi ve HICBIR SEY kaydedilmedi:
        scenario.Host.Database.ChangeTracker.Clear();
        var current = await scenario.Host.Dispatcher.Send(new GetInvoiceByIdRequest(invoice.Id));
        current.PaidAmount.Should().Be(90m, "reddedilen odeme bakiyeyi degistirmemelidir");
        current.Payments.Should().ContainSingle("reddedilen odeme kayda gecmemelidir");
        current.Status.Should().Be(nameof(InvoiceStatus.Finalized), "fatura Paid'e gecmemelidir");
    }

    [RequiresPostgresFact]
    public async Task Paying_a_draft_invoice_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        var act = async () => await scenario.Host.Dispatcher.Send(Payment(draft.Id, 1m));

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("finalize");
    }

    [RequiresPostgresFact]
    public async Task Paying_a_cancelled_invoice_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);
        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = invoice.Id });

        var act = async () => await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 1m));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [RequiresPostgresFact]
    public async Task Paying_an_already_settled_invoice_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);
        await scenario.Host.Dispatcher.Send(Payment(invoice.Id, Gross));

        var act = async () => await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 1m));

        await act.Should().ThrowAsync<ConflictException>();

        // Metin yerine sonuc dogrulanir (bkz. yukaridaki gerekce): kapanmis faturaya gelen
        // odeme ne kaydediliyor ne de bakiyeyi bozuyor.
        scenario.Host.Database.ChangeTracker.Clear();
        var current = await scenario.Host.Dispatcher.Send(new GetInvoiceByIdRequest(invoice.Id));
        current.Status.Should().Be(nameof(InvoiceStatus.Paid));
        current.PaidAmount.Should().Be(Gross);
        current.OutstandingAmount.Should().Be(0m);
        current.Payments.Should().ContainSingle("ikinci odeme kayda gecmemelidir");
    }

    [RequiresPostgresFact]
    public async Task A_cancellation_invoice_cannot_receive_a_payment()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await FinalizedAsync(scenario);
        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = original.Id });

        // Storno negatif tutarlidir; iade akisi bu fazda yok.
        var act = async () => await scenario.Host.Dispatcher.Send(
            Payment(afterCancel.CancelledByInvoiceId!.Value, 1m));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [RequiresPostgresFact]
    public async Task A_payment_dated_in_the_future_is_rejected_as_a_validation_error()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);

        var act = async () => await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = invoice.Id,
            Amount = 10m,
            Method = PaymentMethod.Cash,
            PaidAt = scenario.Clock.UtcNow.AddMinutes(1)
        });

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey("PaidAt");
    }

    [RequiresPostgresFact]
    public async Task A_zero_or_negative_payment_is_rejected_by_validation()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoice = await FinalizedAsync(scenario);

        var zero = async () => await scenario.Host.Dispatcher.Send(Payment(invoice.Id, 0m));
        var negative = async () => await scenario.Host.Dispatcher.Send(Payment(invoice.Id, -5m));

        await zero.Should().ThrowAsync<ValidationException>();
        await negative.Should().ThrowAsync<ValidationException>();
    }
}
