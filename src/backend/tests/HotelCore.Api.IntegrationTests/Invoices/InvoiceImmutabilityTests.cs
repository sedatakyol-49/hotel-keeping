using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.AddPayment;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.Update;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// GoBD §6.1 — <b>kesinlesmis fatura degistirilemez</b>.
/// <para>
/// Iki savunma katmani ayri ayri dogrulanir:
/// <list type="number">
///   <item><b>Handler on kontrolu</b> (<c>InvoicePersistence.EnsureDraft</c>): PUT → 409, anlamli
///         mesajla.</item>
///   <item><b>Persistence guard'i</b> (<c>AppDbContext.EnforceInvoiceImmutability</c>): handler
///         atlansa bile satir ekleme/silme/degistirme ve fatura icerik alanlarinin guncellenmesi
///         <c>SaveChanges</c> sirasinda reddedilir. Bu testler dispatcher'i bilincli olarak
///         BYPASS eder — guard'in gercekten calistigini gostermenin tek yolu budur.</item>
/// </list>
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceImmutabilityTests(PostgresFixture fixture)
{
    private static UpdateInvoiceRequest UpdateWith(Guid invoiceId) => new()
    {
        Id = invoiceId,
        LineItems = [BookingScenario.Line(InvoiceLineType.Extra, "Yeni satir", 1m, 50m)]
    };

    [RequiresPostgresFact]
    public async Task Updating_a_finalized_invoice_is_rejected_as_a_conflict()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        var act = async () => await scenario.Host.Dispatcher.Send(UpdateWith(finalized.Id));

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("Stornorechnung");
    }

    [RequiresPostgresFact]
    public async Task Updating_a_paid_invoice_is_rejected_as_a_conflict()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = finalized.Id,
            Amount = finalized.GrossAmount,
            Method = PaymentMethod.Cash
        });

        var act = async () => await scenario.Host.Dispatcher.Send(UpdateWith(finalized.Id));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [RequiresPostgresFact]
    public async Task Updating_a_cancelled_invoice_is_rejected_as_a_conflict()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();
        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = draft.Id });

        var act = async () => await scenario.Host.Dispatcher.Send(UpdateWith(draft.Id));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [RequiresPostgresFact]
    public async Task A_draft_invoice_can_still_be_updated()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        var updated = await scenario.Host.Dispatcher.Send(UpdateWith(draft.Id));

        // Pozitif kontrol: kural "her fatura kilitli" degil, "kesinlesmis fatura kilitli".
        updated.LineItems.Should().ContainSingle().Which.Description.Should().Be("Yeni satir");
        updated.GrossAmount.Should().Be(50m);
    }

    [RequiresPostgresFact]
    public async Task The_persistence_guard_rejects_adding_a_line_to_a_finalized_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        var database = scenario.Host.Database;

        database.InvoiceLineItems.Add(new InvoiceLineItem
        {
            HotelId = scenario.HotelAId,
            InvoiceId = finalized.Id,
            Type = InvoiceLineType.Extra,
            Description = "Sonradan eklenen satir",
            Quantity = 1m,
            UnitPrice = 10m,
            VatRate = 19m,
            LineNet = 8.40m,
            LineVat = 1.60m
        });

        var act = async () => await database.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain("Kesinlesmis faturanin satirlari degistirilemez");

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task The_persistence_guard_rejects_deleting_a_line_of_a_finalized_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        var database = scenario.Host.Database;

        var line = await database.InvoiceLineItems
            .FirstAsync(candidate => candidate.InvoiceId == finalized.Id);
        database.InvoiceLineItems.Remove(line);

        var act = async () => await database.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain("Kesinlesmis faturanin satirlari degistirilemez");

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task The_persistence_guard_rejects_modifying_a_line_of_a_finalized_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        var database = scenario.Host.Database;

        var line = await database.InvoiceLineItems
            .FirstAsync(candidate => candidate.InvoiceId == finalized.Id);
        line.UnitPrice = 1m;

        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task The_persistence_guard_rejects_changing_the_amount_of_a_finalized_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        var database = scenario.Host.Database;

        var invoice = await database.Invoices.FirstAsync(candidate => candidate.Id == finalized.Id);
        invoice.GrossAmount = 1m;

        var act = async () => await database.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain("Kesinlesmis fatura degistirilemez");
        thrown.Which.Message.Should().Contain(nameof(Invoice.GrossAmount));

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task The_persistence_guard_rejects_deleting_a_finalized_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        // Temiz bir change tracker: varsayilan grafikte faturanin denetim izi kayitlari zaten
        // izlendigi icin Remove() once "iliski koparildi" hatasi verir ve guard'a hic ulasilmaz.
        var database = scenario.CreateApplicationGraph().Database;

        var invoice = await database.Invoices.FirstAsync(candidate => candidate.Id == finalized.Id);
        database.Invoices.Remove(invoice);

        var act = async () => await database.SaveChangesAsync();

        // Silme once soft-delete'e cevrilir; guard bunu "icerik degisikligi" olarak reddeder
        // (GoBD 10 yil saklama: kesinlesmis fatura hicbir yoldan kaybolamaz).
        await act.Should().ThrowAsync<InvalidOperationException>();

        database.ChangeTracker.Clear();
        (await scenario.FindInvoiceAsync(finalized.Id))!.IsDeleted.Should().BeFalse();
    }

    [RequiresPostgresFact]
    public async Task The_persistence_guard_allows_the_finalized_to_paid_status_transition()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        // Pozitif kontrol: guard her degisikligi degil, YALNIZCA icerik degisikligini engeller.
        var paid = await scenario.Host.Dispatcher.Send(new AddInvoicePaymentRequest
        {
            InvoiceId = finalized.Id,
            Amount = finalized.GrossAmount,
            Method = PaymentMethod.Card
        });

        paid.Status.Should().Be(nameof(InvoiceStatus.Paid));
        paid.GrossAmount.Should().Be(finalized.GrossAmount);
        paid.InvoiceNumber.Should().Be(finalized.InvoiceNumber);
    }
}
