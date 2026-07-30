using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Fatura uclarinin izin denetimi (architecture.md §7 — policy adi = izin anahtari).
/// <para>
/// Her negatif iddianin yaninda <b>pozitif kontrol</b> vardir: ayni istek, yalnizca eksik izin
/// eklenerek tekrarlanir. Aksi halde 403'un izinden mi yoksa baska bir hatadan mi geldigi
/// belirsiz kalirdi.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoicesAuthorizationTests(PostgresFixture fixture)
{
    private static readonly string[] AllInvoicePermissions =
    [
        Permissions.InvoicesView,
        Permissions.InvoicesCreate,
        Permissions.InvoicesApprove,
        Permissions.InvoicesCancel
    ];

    private static Uri Invoices { get; } = new("api/v1/invoices", UriKind.Relative);

    private static Uri FinalizeOf(Guid id) => new($"api/v1/invoices/{id}/finalize", UriKind.Relative);

    private static Uri CancelOf(Guid id) => new($"api/v1/invoices/{id}/cancel", UriKind.Relative);

    private static Uri PaymentsOf(Guid id) => new($"api/v1/invoices/{id}/payments", UriKind.Relative);

    private static Uri PdfOf(Guid id) => new($"api/v1/invoices/{id}/pdf", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Listing_invoices_without_the_view_permission_is_forbidden()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var withoutView = scenario.CreateClient([Permissions.InvoicesCreate]);
        using var withView = scenario.CreateClient([Permissions.InvoicesView]);

        using var denied = await withoutView.GetAsync(Invoices);
        using var allowed = await withView.GetAsync(Invoices);

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_draft_without_the_create_permission_is_forbidden()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var withoutCreate = scenario.CreateClient([Permissions.InvoicesView]);
        using var withCreate = scenario.CreateClient([Permissions.InvoicesView, Permissions.InvoicesCreate]);

        var body = DraftBody(scenario);

        using var denied = await withoutCreate.PostAsJsonAsync(Invoices, body);
        using var allowed = await withCreate.PostAsJsonAsync(Invoices, body);

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [RequiresPostgresFact]
    public async Task Finalizing_without_the_approve_permission_is_forbidden()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        using var withoutApprove = scenario.CreateClient([Permissions.InvoicesView, Permissions.InvoicesCreate]);
        using var withApprove = scenario.CreateClient(AllInvoicePermissions);

        using var denied = await withoutApprove.PostAsync(FinalizeOf(draft.Id), content: null);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden, "kesinlestirme Invoices.Approve ister");

        using var allowed = await withApprove.PostAsync(FinalizeOf(draft.Id), content: null);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Cancelling_without_the_cancel_permission_is_forbidden()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        using var withoutCancel = scenario.CreateClient(
            [Permissions.InvoicesView, Permissions.InvoicesCreate, Permissions.InvoicesApprove]);
        using var withCancel = scenario.CreateClient(AllInvoicePermissions);

        using var denied = await withoutCancel.PostAsJsonAsync(CancelOf(finalized.Id), new { reason = "Test" });
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden, "iptal Invoices.Cancel ister");

        using var allowed = await withCancel.PostAsJsonAsync(CancelOf(finalized.Id), new { reason = "Test" });
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Recording_a_payment_without_the_create_permission_is_forbidden()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        var payment = new { method = nameof(PaymentMethod.Cash), amount = finalized.GrossAmount };

        using var withoutCreate = scenario.CreateClient([Permissions.InvoicesView]);
        using var withCreate = scenario.CreateClient(AllInvoicePermissions);

        using var denied = await withoutCreate.PostAsJsonAsync(PaymentsOf(finalized.Id), payment);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowed = await withCreate.PostAsJsonAsync(PaymentsOf(finalized.Id), payment);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Every_invoice_endpoint_requires_authentication()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        using var anonymous = scenario.CreateAnonymousClient();

        using var list = await anonymous.GetAsync(Invoices);
        using var detail = await anonymous.GetAsync(new Uri($"api/v1/invoices/{finalized.Id}", UriKind.Relative));
        using var create = await anonymous.PostAsJsonAsync(Invoices, DraftBody(scenario));
        using var finalize = await anonymous.PostAsync(FinalizeOf(finalized.Id), content: null);
        using var cancel = await anonymous.PostAsJsonAsync(CancelOf(finalized.Id), new { });
        using var pdf = await anonymous.GetAsync(PdfOf(finalized.Id));

        new[] { list, detail, create, finalize, cancel, pdf }
            .Should().AllSatisfy(response =>
                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    [RequiresPostgresFact]
    public async Task The_pdf_endpoint_reports_not_implemented_instead_of_faking_a_document()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        using var client = scenario.CreateClient(AllInvoicePermissions);

        using var response = await client.GetAsync(PdfOf(finalized.Id));

        // Sahte/bos bir PDF dondurmek denetim acisindan yaniltici olurdu.
        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem!.Status.Should().Be((int)HttpStatusCode.NotImplemented);
        problem.Title.Should().Contain("PDF");
    }

    [RequiresPostgresFact]
    public async Task The_pdf_endpoint_still_requires_the_view_permission()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();
        using var withoutView = scenario.CreateClient([Permissions.InvoicesCreate]);

        using var response = await withoutView.GetAsync(PdfOf(finalized.Id));

        // 501 "uygulanmadi" cevabi izin denetiminin ONUNE gecmemelidir.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static object DraftBody(BookingScenario scenario) => new
    {
        guestId = scenario.GuestAId,
        lineItems = new[]
        {
            new
            {
                type = nameof(InvoiceLineType.Extra),
                description = "Minibar",
                quantity = 1m,
                unitPrice = 10m
            }
        }
    };
}
