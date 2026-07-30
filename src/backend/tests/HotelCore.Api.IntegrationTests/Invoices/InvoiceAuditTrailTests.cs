using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.AddPayment;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.GetById;
using HotelCore.Application.Features.Invoices.Update;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// GoBD §6.3 — <b>denetim izi</b> (append-only).
///
/// <para><b>SIRALAMA TUZAGI — bilincli olarak sira iddiasi kurulmaz:</b>
/// <c>PaymentRecorded</c> ve <c>Paid</c> ayni <c>SaveChanges</c> icinde yazilir ve
/// <c>PerformedAt</c> degerini ayni saatten alir. Testlerde saat DONDURULMUS oldugu icin iki
/// kaydin zaman damgasi <b>birebir esittir</b>; okuma yolu esitlikte <c>Id</c>'ye (rastgele Guid)
/// duser. Bu yuzden testler izin <i>icerigini</i> (hangi aksiyonlar, kac kez, hangi ayrintiyla)
/// dogrular. Domain'de monoton artan bir sira alani olmadigi icin "hangi kayit once yazildi"
/// sorusu bugun veriyle cevaplanamaz; bu bir <b>tasarim eksigidir</b> ve raporlanmistir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceAuditTrailTests(PostgresFixture fixture)
{
    private static string[] ActionsOf(IEnumerable<InvoiceAuditEntry> entries) =>
        [.. entries.Select(entry => entry.Action.ToString()).Order(StringComparer.Ordinal)];

    private static JsonElement DetailsOf(InvoiceAuditEntry entry) =>
        JsonDocument.Parse(entry.Details!).RootElement;

    [RequiresPostgresFact]
    public async Task Creating_an_invoice_writes_a_created_entry_with_the_amounts()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var draft = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 2m, 10m));

        var entries = await scenario.ListAuditEntriesAsync(draft.Id);
        var created = entries.Should().ContainSingle().Which;

        created.Action.Should().Be(InvoiceAuditAction.Created);
        created.PerformedByUserId.Should().Be(scenario.Host.CurrentUser.UserId);
        created.PerformedAt.Should().Be(scenario.Clock.UtcNow);

        var details = DetailsOf(created);
        details.GetProperty("source").GetString().Should().Be("manual");
        details.GetProperty("grossAmount").GetDecimal().Should().Be(20m);
        details.GetProperty("lineCount").GetInt32().Should().Be(1);
    }

    [RequiresPostgresFact]
    public async Task Updating_a_draft_records_the_changed_fields_with_old_and_new_amounts()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m));

        await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = draft.Id,
            LineItems =
            [
                BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m),
                BookingScenario.Line(InvoiceLineType.Extra, "Parkplatz", 1m, 15m)
            ]
        });

        var entries = await scenario.ListAuditEntriesAsync(draft.Id);
        ActionsOf(entries).Should().Equal(
            nameof(InvoiceAuditAction.Created),
            nameof(InvoiceAuditAction.Updated));

        var updated = entries.Single(entry => entry.Action is InvoiceAuditAction.Updated);
        var details = DetailsOf(updated);

        details.GetProperty("changedFields").EnumerateArray()
            .Select(field => field.GetString())
            .Should().Contain(["lineItems", "lineCount", "netAmount", "vatAmount", "grossAmount"]);

        details.GetProperty("grossAmount").GetProperty("old").GetDecimal().Should().Be(10m);
        details.GetProperty("grossAmount").GetProperty("new").GetDecimal().Should().Be(25m);
        details.GetProperty("lineCount").GetProperty("old").GetInt32().Should().Be(1);
        details.GetProperty("lineCount").GetProperty("new").GetInt32().Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task Finalizing_records_the_assigned_number_and_the_issue_date()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        var entries = await scenario.ListAuditEntriesAsync(finalized.Id);
        ActionsOf(entries).Should().Equal(
            nameof(InvoiceAuditAction.Created),
            nameof(InvoiceAuditAction.Finalized));

        var details = DetailsOf(entries.Single(entry => entry.Action is InvoiceAuditAction.Finalized));
        details.GetProperty("invoiceNumber").GetString().Should().Be(finalized.InvoiceNumber);
    }

    [RequiresPostgresFact]
    public async Task A_partial_payment_records_only_the_collection_event()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 100m));

        await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = finalized.Id,
            Amount = 40m,
            Method = PaymentMethod.Cash
        });

        var entries = await scenario.ListAuditEntriesAsync(finalized.Id);
        ActionsOf(entries).Should().Equal(
            nameof(InvoiceAuditAction.Created),
            nameof(InvoiceAuditAction.Finalized),
            nameof(InvoiceAuditAction.PaymentRecorded));

        var details = DetailsOf(entries.Single(entry => entry.Action is InvoiceAuditAction.PaymentRecorded));
        details.GetProperty("amount").GetDecimal().Should().Be(40m);
        details.GetProperty("totalPaid").GetDecimal().Should().Be(40m);
        details.GetProperty("outstandingAmount").GetDecimal().Should().Be(60m);
    }

    [RequiresPostgresFact]
    public async Task Settling_the_balance_records_the_collection_event_AND_a_separate_paid_entry()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 100m));

        await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = finalized.Id,
            Amount = 40m,
            Method = PaymentMethod.Cash
        });
        await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = finalized.Id,
            Amount = 60m,
            Method = PaymentMethod.Card
        });

        var entries = await scenario.ListAuditEntriesAsync(finalized.Id);

        // Her tahsilat bir PaymentRecorded uretir; Paid YALNIZCA bakiye kapaninca ve AYRI bir
        // kayit olarak yazilir. Sira iddiasi YOK — iki kayit ayni PerformedAt'e duser (sinif notu).
        entries.Count(entry => entry.Action is InvoiceAuditAction.PaymentRecorded).Should().Be(2);
        entries.Count(entry => entry.Action is InvoiceAuditAction.Paid).Should().Be(1);

        var paid = entries.Single(entry => entry.Action is InvoiceAuditAction.Paid);
        var details = DetailsOf(paid);
        details.GetProperty("previousStatus").GetString().Should().Be(nameof(InvoiceStatus.Finalized));
        details.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Paid));
        details.GetProperty("totalPaid").GetDecimal().Should().Be(100m);

        // Bakiyeyi kapatan odeme, ayni SaveChanges icindeki iki kaydin ORTAK kimligidir.
        var settlingPaymentId = details.GetProperty("settledByPaymentId").GetGuid();
        entries
            .Where(entry => entry.Action is InvoiceAuditAction.PaymentRecorded)
            .Select(entry => DetailsOf(entry).GetProperty("paymentId").GetGuid())
            .Should().Contain(settlingPaymentId);
    }

    [RequiresPostgresFact]
    public async Task Cancelling_a_draft_records_that_no_storno_was_required()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = draft.Id, Reason = "Mukerrer." });

        var entries = await scenario.ListAuditEntriesAsync(draft.Id);
        var cancelled = entries.Single(entry => entry.Action is InvoiceAuditAction.Cancelled);
        var details = DetailsOf(cancelled);

        details.GetProperty("previousStatus").GetString().Should().Be(nameof(InvoiceStatus.Draft));
        details.GetProperty("stornoRequired").GetBoolean().Should().BeFalse();
        details.GetProperty("reason").GetString().Should().Be("Mukerrer.");
    }

    [RequiresPostgresFact]
    public async Task Cancelling_a_finalized_invoice_leaves_a_trail_on_both_documents()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var original = await scenario.CreateFinalizedInvoiceAsync();

        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest
        {
            Id = original.Id,
            Reason = "Fiyat hatasi."
        });
        var stornoId = afterCancel.CancelledByInvoiceId!.Value;

        var originalEntries = await scenario.ListAuditEntriesAsync(original.Id);
        ActionsOf(originalEntries).Should().Equal(
            nameof(InvoiceAuditAction.Cancelled),
            nameof(InvoiceAuditAction.Created),
            nameof(InvoiceAuditAction.Finalized));

        // Storno kendi belgesi olarak olusturuldu + kesinlestirildi izini tasir.
        ActionsOf(await scenario.ListAuditEntriesAsync(stornoId)).Should().Equal(
            nameof(InvoiceAuditAction.Created),
            nameof(InvoiceAuditAction.Finalized));

        var cancelDetails = DetailsOf(
            originalEntries.Single(entry => entry.Action is InvoiceAuditAction.Cancelled));
        cancelDetails.GetProperty("stornoRequired").GetBoolean().Should().BeTrue();
        cancelDetails.GetProperty("cancelledByInvoiceId").GetGuid().Should().Be(stornoId);
        cancelDetails.GetProperty("reason").GetString().Should().Be("Fiyat hatasi.");
    }

    [RequiresPostgresFact]
    public async Task A_rejected_operation_leaves_no_audit_entry_behind()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 100m));

        var before = (await scenario.ListAuditEntriesAsync(finalized.Id)).Count;

        // Fazla odeme reddedilir: iz de yazilmamalidir (iz ile islem AYNI transaction'dadir).
        var act = async () => await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = finalized.Id,
            Amount = 100.01m,
            Method = PaymentMethod.Cash
        });

        await act.Should().ThrowAsync<ConflictException>();

        scenario.Host.Database.ChangeTracker.Clear();
        (await scenario.ListAuditEntriesAsync(finalized.Id)).Should().HaveCount(before);
    }

    [RequiresPostgresFact]
    public async Task The_audit_trail_is_exposed_on_the_invoice_detail_response()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        var detail = await scenario.Host.Dispatcher.Send(new GetInvoiceByIdRequest(finalized.Id));

        detail.AuditTrail.Select(entry => entry.Action).Should().BeEquivalentTo(
            [nameof(InvoiceAuditAction.Created), nameof(InvoiceAuditAction.Finalized)]);
        detail.AuditTrail.Should().AllSatisfy(entry =>
            entry.PerformedByUserId.Should().Be(scenario.Host.CurrentUser.UserId));
    }
}
