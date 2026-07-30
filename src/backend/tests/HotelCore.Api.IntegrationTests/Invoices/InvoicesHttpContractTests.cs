using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Fatura uclarinin <b>HTTP sozlesmesi</b> (docs/api-contracts-invoices.md): durum kodlari,
/// govde alanlari ve GoBD kurallarinin uctan uca gorunumu.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoicesHttpContractTests(PostgresFixture fixture)
{
    private static readonly string[] InvoiceClerkPermissions =
    [
        Permissions.InvoicesView,
        Permissions.InvoicesCreate,
        Permissions.InvoicesApprove,
        Permissions.InvoicesCancel
    ];

    private static Uri Invoices { get; } = new("api/v1/invoices", UriKind.Relative);

    private static Uri InvoiceOf(Guid id) => new($"api/v1/invoices/{id}", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Creating_a_draft_returns_201_with_a_location_header_and_no_number()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);

        using var response = await client.PostAsJsonAsync(Invoices, new
        {
            guestId = scenario.GuestAId,
            culture = "de",
            lineItems = new[]
            {
                new { type = nameof(InvoiceLineType.Extra), description = "Minibar", quantity = 2m, unitPrice = 10m }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<InvoiceDetailResponse>();
        created!.InvoiceNumber.Should().BeNull("taslak numara almaz");
        created.Status.Should().Be(nameof(InvoiceStatus.Draft));
        created.Culture.Should().Be("de");
        created.Currency.Should().Be("EUR");
        created.GrossAmount.Should().Be(20m);
        created.OutstandingAmount.Should().Be(20m);
        created.AuditTrail.Should().ContainSingle()
            .Which.Action.Should().Be(nameof(InvoiceAuditAction.Created));
    }

    [RequiresPostgresFact]
    public async Task The_client_cannot_dictate_the_vat_rate_or_the_totals()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);

        // Govdeye fazladan alanlar konur: sozlesmede olmadiklari icin sessizce DUSURULMELIDIR.
        using var response = await client.PostAsJsonAsync(Invoices, new
        {
            guestId = scenario.GuestAId,
            netAmount = 1m,
            vatAmount = 0m,
            grossAmount = 1m,
            lineItems = new[]
            {
                new
                {
                    type = nameof(InvoiceLineType.Extra),
                    description = "Minibar",
                    quantity = 1m,
                    unitPrice = 119m,
                    vatRate = 0m,
                    lineNet = 119m,
                    lineVat = 0m
                }
            }
        });

        var created = await response.Content.ReadFromJsonAsync<InvoiceDetailResponse>();

        created!.LineItems.Should().ContainSingle().Which.VatRate
            .Should().Be(19m, "oran otelin TaxProfile'indan cozulur, istemciden alinmaz");
        created.NetAmount.Should().Be(100m);
        created.VatAmount.Should().Be(19m);
        created.GrossAmount.Should().Be(119m);
    }

    [RequiresPostgresFact]
    public async Task Finalizing_assigns_a_gapless_number_and_locks_the_document_over_http()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);
        var draft = await scenario.CreateManualInvoiceAsync();

        using var finalizeResponse = await client.PostAsync(
            new Uri($"api/v1/invoices/{draft.Id}/finalize", UriKind.Relative),
            content: null);

        finalizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalized = await finalizeResponse.Content.ReadFromJsonAsync<InvoiceDetailResponse>();
        finalized!.InvoiceNumber.Should().Be(scenario.InvoiceNumber(1));
        finalized.IssuedAt.Should().NotBeNull();

        // GoBD §6.1: kesinlesmis belgeye PUT → 409.
        using var updateResponse = await client.PutAsJsonAsync(InvoiceOf(draft.Id), new
        {
            lineItems = new[]
            {
                new { type = nameof(InvoiceLineType.Extra), description = "Degistirme denemesi", quantity = 1m, unitPrice = 1m }
            }
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [RequiresPostgresFact]
    public async Task There_is_no_delete_endpoint_for_invoices()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        using var response = await client.DeleteAsync(InvoiceOf(finalized.Id));

        // 10 yil saklama: DELETE ucu BILINCLI olarak yoktur.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [RequiresPostgresFact]
    public async Task Cancelling_a_finalized_invoice_returns_the_original_linked_to_its_storno()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);
        var original = await scenario.CreateFinalizedInvoiceAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri($"api/v1/invoices/{original.Id}/cancel", UriKind.Relative),
            new { reason = "Fiyat hatasi." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<InvoiceDetailResponse>();

        cancelled!.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        cancelled.GrossAmount.Should().Be(original.GrossAmount);
        cancelled.CancelledByInvoiceId.Should().NotBeNull();

        var storno = await client.GetFromJsonAsync<InvoiceDetailResponse>(
            InvoiceOf(cancelled.CancelledByInvoiceId!.Value));

        storno!.IsCancellationInvoice.Should().BeTrue();
        storno.CancelsInvoiceId.Should().Be(original.Id);
        storno.GrossAmount.Should().Be(-original.GrossAmount);
    }

    [RequiresPostgresFact]
    public async Task An_overpayment_is_reported_as_409_over_http()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);
        var finalized = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 100m));

        var payments = new Uri($"api/v1/invoices/{finalized.Id}/payments", UriKind.Relative);

        using var partial = await client.PostAsJsonAsync(payments, new { method = nameof(PaymentMethod.Cash), amount = 60m });
        using var tooMuch = await client.PostAsJsonAsync(payments, new { method = nameof(PaymentMethod.Card), amount = 40.01m });
        using var exact = await client.PostAsJsonAsync(payments, new { method = nameof(PaymentMethod.Card), amount = 40m });

        partial.StatusCode.Should().Be(HttpStatusCode.OK);
        tooMuch.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exact.StatusCode.Should().Be(HttpStatusCode.OK);

        var settled = await exact.Content.ReadFromJsonAsync<InvoiceDetailResponse>();
        settled!.Status.Should().Be(nameof(InvoiceStatus.Paid));
        settled.OutstandingAmount.Should().Be(0m);
    }

    [RequiresPostgresFact]
    public async Task The_detail_response_exposes_lines_payments_and_the_audit_trail_in_camel_case()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);
        var finalized = await scenario.CreateFinalizedInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 100m));

        using var payment = await client.PostAsJsonAsync(
            new Uri($"api/v1/invoices/{finalized.Id}/payments", UriKind.Relative),
            new { method = nameof(PaymentMethod.Cash), amount = 100m });
        payment.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await client.GetStringAsync(InvoiceOf(finalized.Id)));
        var root = document.RootElement;

        root.GetProperty("invoiceNumber").GetString().Should().Be(scenario.InvoiceNumber(1));
        root.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Paid));
        root.GetProperty("lineItems").GetArrayLength().Should().Be(1);
        root.GetProperty("payments").GetArrayLength().Should().Be(1);

        // Denetim izi: Created + Finalized + PaymentRecorded + Paid (sira iddiasi YOK).
        root.GetProperty("auditTrail").EnumerateArray()
            .Select(entry => entry.GetProperty("action").GetString())
            .Should().BeEquivalentTo(
            [
                nameof(InvoiceAuditAction.Created),
                nameof(InvoiceAuditAction.Finalized),
                nameof(InvoiceAuditAction.PaymentRecorded),
                nameof(InvoiceAuditAction.Paid)
            ]);
    }

    [RequiresPostgresFact]
    public async Task Listing_supports_status_and_search_filters()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions);
        await scenario.CreateManualInvoiceAsync();
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        var byStatus = await client.GetFromJsonAsync<PagedResult<InvoiceResponse>>(
            new Uri("api/v1/invoices?status=Finalized", UriKind.Relative));
        var byNumber = await client.GetFromJsonAsync<PagedResult<InvoiceResponse>>(
            new Uri($"api/v1/invoices?search={finalized.InvoiceNumber}", UriKind.Relative));

        byStatus!.Items.Should().ContainSingle().Which.Id.Should().Be(finalized.Id);
        byNumber!.Items.Should().ContainSingle().Which.Id.Should().Be(finalized.Id);
    }
}
