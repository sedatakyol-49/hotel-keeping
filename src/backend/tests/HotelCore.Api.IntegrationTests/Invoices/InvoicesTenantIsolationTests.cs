using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Invoices.Create;
using HotelCore.Application.Features.Invoices.Finalize;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// Faturalarda multi-tenant izolasyon (architecture.md §3):
/// <list type="bullet">
///   <item>baska otelin faturasi <b>404</b>'tur — 403 DEGIL: kaydin var oldugu bilgisi bile
///         sizdirilmaz,</item>
///   <item><c>X-Hotel-Id</c> ile erisilemeyen otele gecis <b>403</b>'tur ve uc hic calismaz,</item>
///   <item>liste ve toplam sayac yalnizca aktif oteli gorur,</item>
///   <item>Head Office konsolide modunda (aktif otel yok) fatura <b>yazilamaz</b> → 400.</item>
/// </list>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoicesTenantIsolationTests(PostgresFixture fixture)
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

    /// <summary>B otelinde kesinlesmis bir fatura uretir (B otelinin uygulama baglamiyla).</summary>
    private static async Task<InvoiceDetailResponse> CreateInvoiceInHotelBAsync(BookingScenario scenario)
    {
        var hotelB = scenario.CreateApplicationGraph(activeHotelId: scenario.HotelBId);

        var draft = await hotelB.Dispatcher.Send(new CreateInvoiceRequest
        {
            GuestId = scenario.GuestBId,
            LineItems = [BookingScenario.Line(InvoiceLineType.Extra, "Minibar B", 1m, 10m)]
        });

        return await hotelB.Dispatcher.Send(new FinalizeInvoiceRequest(draft.Id));
    }

    [RequiresPostgresFact]
    public async Task An_invoice_of_another_hotel_is_reported_as_404_not_403()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoiceInB = await CreateInvoiceInHotelBAsync(scenario);

        using var client = scenario.CreateClient(InvoiceClerkPermissions, [scenario.HotelAId]);

        using var response = await client.GetAsync(InvoiceOf(invoiceInB.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Finalizing_or_cancelling_an_invoice_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoiceInB = await CreateInvoiceInHotelBAsync(scenario);

        using var client = scenario.CreateClient(InvoiceClerkPermissions, [scenario.HotelAId]);

        using var finalize = await client.PostAsync(
            new Uri($"api/v1/invoices/{invoiceInB.Id}/finalize", UriKind.Relative), content: null);
        using var cancel = await client.PostAsJsonAsync(
            new Uri($"api/v1/invoices/{invoiceInB.Id}/cancel", UriKind.Relative), new { });

        finalize.StatusCode.Should().Be(HttpStatusCode.NotFound);
        cancel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Switching_to_a_hotel_the_user_cannot_access_is_rejected_with_403()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        await CreateInvoiceInHotelBAsync(scenario);

        using var client = scenario.CreateClient(
            InvoiceClerkPermissions,
            [scenario.HotelAId],
            activeHotelId: scenario.HotelBId);

        using var response = await client.GetAsync(Invoices);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_returns_only_the_invoices_of_the_active_hotel()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var invoiceInA = await scenario.CreateFinalizedInvoiceAsync();
        await CreateInvoiceInHotelBAsync(scenario);

        using var client = scenario.CreateClient(InvoiceClerkPermissions, [scenario.HotelAId]);

        var page = await client.GetFromJsonAsync<PagedResult<InvoiceResponse>>(Invoices);

        page!.Items.Select(invoice => invoice.Id).Should().Equal(invoiceInA.Id);
        page.TotalCount.Should().Be(1, "toplam sayac da tenant filtresine tabidir");
    }

    [RequiresPostgresFact]
    public async Task An_invoice_cannot_be_issued_to_a_guest_of_another_hotel()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(InvoiceClerkPermissions, [scenario.HotelAId]);

        using var response = await client.PostAsJsonAsync(Invoices, new
        {
            guestId = scenario.GuestBId,
            lineItems = new[]
            {
                new { type = nameof(InvoiceLineType.Extra), description = "Sizinti", quantity = 1m, unitPrice = 10m }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Writing_in_consolidated_head_office_mode_is_rejected_with_400()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        // allHotels = true ve X-Hotel-Id YOK → hangi otele yazilacagi belirsiz.
        using var client = scenario.CreateClient(
            InvoiceClerkPermissions,
            hotelIds: [],
            canAccessAllHotels: true);

        using var response = await client.PostAsJsonAsync(Invoices, new
        {
            guestId = scenario.GuestAId,
            lineItems = new[]
            {
                new { type = nameof(InvoiceLineType.Extra), description = "Minibar", quantity = 1m, unitPrice = 10m }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
