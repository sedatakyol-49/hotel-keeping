using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Invoices.Update;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// <c>PUT /api/v1/invoices/{id}</c> <b>kapsaminin</b> faturanin kaynagina gore degistigini
/// kilitler.
///
/// <para><b>Kilitlenen hata:</b> rezervasyondan uretilen 486,00 EUR'luk bir taslaga tek bir
/// <c>Extra</c> PUT'lamak, "tam degisim" semantigi yuzunden sunucunun urettigi konaklama satirini
/// ve Kurtaxe'yi <b>sessizce siliyor</b>, fatura 108,00 EUR'ya dusuyordu. Kesinlestikten sonra da
/// duzeltilemiyordu (GoBD).</para>
///
/// <para><b>Yeni kural:</b>
/// <list type="bullet">
///   <item><b>Rezervasyon faturasi:</b> govde yalnizca faturanin kendi <c>Extra</c> satirlarini
///   degistirir; <c>RoomCharge</c> ve <c>CityTax</c> sunucunundur ve korunur. Govdede
///   <c>RoomCharge</c>/<c>CityTax</c> gelirse <b>400</b> (<c>LineItems</c> anahtari) — sessizce
///   yok saymak kullaniciya gonderdiginin kaydedildigini dusundururdu.</item>
///   <item><b>Elle kesilen fatura:</b> tam degisim semantigi <b>aynen korunur</b> — bos govde
///   satirsiz bir belge uretecegi icin <b>400</b>.</item>
/// </list></para>
///
/// <para><b>Neden dispatcher seviyesi:</b> iddialar hata <b>anahtarina</b> (<c>LineItems</c>)
/// bakar; mesaj metni yerellestirilmistir ve makinenin kulturune gore degisir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceUpdateScopeTests(PostgresFixture fixture)
{
    /// <summary>2 gece x 120,00 konaklama.</summary>
    private const decimal RoomChargeGross = 2 * BookingScenario.BasePrice;

    /// <summary>2 yetiskin x 2 gece x 3,00 Kurtaxe.</summary>
    private const decimal CityTaxGross = 12m;

    [RequiresPostgresFact]
    public async Task Adding_one_extra_to_a_reservation_invoice_preserves_the_server_owned_lines()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var (reservation, draft) = await CreateReservationDraftAsync(scenario);

        draft.GrossAmount.Should().Be(RoomChargeGross + CityTaxGross);

        var updated = await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = draft.Id,
            LineItems = [BookingScenario.Line(InvoiceLineType.Extra, "Fruehstueck", 2m, 9m)]
        });

        var roomCharges = RoomCharges(updated);
        roomCharges.Should().ContainSingle("konaklama satiri PUT'un kapsaminda degildir");
        roomCharges[0].LineGross.Should().Be(
            reservation.TotalAmount,
            "oda ucreti reservation.TotalAmount'a kurusu kurusuna esittir");

        updated.LineItems.Should().ContainSingle(line => line.Type == nameof(InvoiceLineType.CityTax));
        updated.CityTaxAmount.Should().Be(CityTaxGross, "Kurtaxe sunucunundur ve korunur");

        updated.LineItems.Should().ContainSingle(line => line.Type == nameof(InvoiceLineType.Extra));
        updated.GrossAmount.Should().Be(
            RoomChargeGross + CityTaxGross + 18m,
            "ekstra EKLENIR, sunucunun satirlarinin yerine gecmez");

        // Belge sirasi: konaklama -> ekstralar -> Kurtaxe (yanit SortOrder'a gore siralidir).
        updated.LineItems.Select(line => line.Type).Should().Equal(
            nameof(InvoiceLineType.RoomCharge),
            nameof(InvoiceLineType.Extra),
            nameof(InvoiceLineType.CityTax));
    }

    [RequiresPostgresFact]
    public async Task Sending_a_room_charge_to_a_reservation_invoice_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var (_, draft) = await CreateReservationDraftAsync(scenario);

        var act = async () => await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = draft.Id,
            LineItems =
            [
                BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m),
                BookingScenario.Line(InvoiceLineType.RoomCharge, "Ikinci konaklama satiri", 1m, 500m)
            ]
        });

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey(
            "LineItems",
            "hata istemcinin gonderdigi alana baglanmalidir (mesaj metni yerellestirilmistir)");

        // Kismi yazma olmadi: fatura dokunulmadan durur.
        var stored = (await scenario.FindInvoiceAsync(draft.Id))!;
        stored.GrossAmount.Should().Be(RoomChargeGross + CityTaxGross);
    }

    [RequiresPostgresFact]
    public async Task An_empty_body_clears_only_the_manually_added_extras_of_a_reservation_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var (reservation, draft) = await CreateReservationDraftAsync(scenario);

        var withExtra = await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = draft.Id,
            LineItems = [BookingScenario.Line(InvoiceLineType.Extra, "Parkplatz", 2m, 12m)]
        });

        withExtra.GrossAmount.Should().Be(RoomChargeGross + CityTaxGross + 24m);

        // Bos dizi rezervasyon faturasinda MESRU bir istektir: "elle eklenen tum ekstralari kaldir".
        var cleared = await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = draft.Id,
            LineItems = []
        });

        cleared.LineItems.Should().NotContain(line => line.Type == nameof(InvoiceLineType.Extra));

        var roomCharges = RoomCharges(cleared);
        roomCharges.Should().ContainSingle("sunucunun satirlari yerinde durur");
        roomCharges[0].LineGross.Should().Be(reservation.TotalAmount);
        cleared.CityTaxAmount.Should().Be(CityTaxGross);
        cleared.GrossAmount.Should().Be(RoomChargeGross + CityTaxGross);
    }

    [RequiresPostgresFact]
    public async Task An_empty_body_is_rejected_for_a_manual_invoice()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var manual = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m));

        var act = async () => await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = manual.Id,
            LineItems = []
        });

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey("LineItems");

        // Tam degisim semantigi korunur: elle kesilen fatura satirsiz kalamaz.
        var stored = (await scenario.FindInvoiceAsync(manual.Id))!;
        stored.GrossAmount.Should().Be(10m);
    }

    /// <summary>
    /// Elle kesilen faturada PUT <b>tam degisimdir</b>: gonderilen satir kumesi eskisinin
    /// yerine gecer. Rezervasyon faturasindaki daraltilmis kapsam bu davranisi degistirmemelidir.
    /// </summary>
    [RequiresPostgresFact]
    public async Task A_manual_invoice_still_replaces_all_of_its_lines()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var manual = await scenario.CreateManualInvoiceAsync(
            BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m),
            BookingScenario.Line(InvoiceLineType.Extra, "Parkplatz", 1m, 10m));

        var updated = await scenario.Host.Dispatcher.Send(new UpdateInvoiceRequest
        {
            Id = manual.Id,
            LineItems = [BookingScenario.Line(InvoiceLineType.RoomCharge, "Konaklama", 1m, 100m)]
        });

        updated.LineItems.Should().ContainSingle()
            .Which.Type.Should().Be(nameof(InvoiceLineType.RoomCharge));
        updated.GrossAmount.Should().Be(100m);
    }

    private static async Task<(ReservationResponse Reservation, InvoiceDetailResponse Draft)>
        CreateReservationDraftAsync(BookingScenario scenario)
    {
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));

        var draft = await scenario.CreateReservationInvoiceAsync(reservation.Id);

        return (reservation, draft);
    }

    private static List<InvoiceLineItemResponse> RoomCharges(InvoiceDetailResponse invoice) =>
        invoice.LineItems
            .Where(line => line.Type == nameof(InvoiceLineType.RoomCharge))
            .ToList();
}
