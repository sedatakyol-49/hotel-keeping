using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Reports.Common;
using HotelCore.Application.Features.Reports.GetRevenueReport;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Reports;

/// <summary>
/// Ciro raporunun <b>muhasebe degismezi</b>: hicbir konaklama tutari iki kova arasinda
/// kaybolmaz.
///
/// <para><b>Kilitlenen hata:</b> kesinlesmis bir fatura iptal edilip Stornorechnung kesildiginde
/// konaklama "faturalanmis" sayiliyordu (kosul yalnizca <c>IssuedAt != null</c> idi). Sonuc:
/// orijinal (+X) ile storno (−X) ciroda birbirini sifirliyor, konaklama "faturalanmis" sayildigi
/// icin <c>unbilledRoomRevenueGross</c>'a da girmiyordu — tutar <b>rapordan tumuyle
/// kayboluyordu</b>. Duzeltmeden sonra "yururlukteki belge" tanimi
/// (<c>InvoiceEffectiveness.IsEffectiveDocument</c>) iptal edilmis faturayi ve storno'yu birlikte
/// disliyor, konaklama yeniden "faturalanmamis" oluyor.</para>
///
/// <para><b>Degismez nasil kuruluyor:</b> raporun iki para tarafi
/// (<c>totalRevenue.gross</c> — kesinlesmis faturalardan gelen gelir — ve
/// <c>unbilledRoomRevenueGross</c> — henuz faturalanmamis konaklama tutari) birlikte
/// <c>Σ reservation.TotalAmount</c> etmelidir. Degismezin <b>gecerlilik kosullari</b> testte
/// bilincli olarak saglanir:
/// <list type="bullet">
///   <item>konaklamalar rapor penceresinin <b>tamamen</b> icindedir (kismi kirpma yok, aksi hâlde
///   her iki taraf da orantili paylara bolunurdu),</item>
///   <item>hicbir faturada <c>Extra</c> satir yoktur (ekstra geliri
///   <c>reservation.TotalAmount</c>'ta bulunmaz; olsaydi degismez dogal olarak ekstra kadar
///   sapardi),</item>
///   <item>Kurtaxe zaten ciro degildir ve ayri alanda durur.</item>
/// </list>
/// Bu kosullar altinda esitlik <b>tam</b> olmalidir; kurus sapmasi bile tanimlarin ayristigini
/// gosterir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RevenueRecognitionTests(PostgresFixture fixture)
{
    /// <summary>2 gece x 120,00 — <c>BookingScenario</c>'nun varsayilan konaklama tutari.</summary>
    private const decimal StayGross = 2 * BookingScenario.BasePrice;

    /// <summary>2 yetiskin x 2 gece x 3,00 Kurtaxe.</summary>
    private const decimal StayCityTax = 12m;

    [RequiresPostgresFact]
    public async Task A_stornoed_stay_appears_in_unbilled_and_contributes_zero_revenue()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        // Ardisik ama AYRIK iki konaklama: rapor penceresi ikisini ayri ayri secebilsin diye
        // geceler ust uste binmez ve gruplama anahtari (giris, cikis, kanal) farklidir.
        var start = scenario.Today.AddDays(10);
        var billedStay = await scenario.CreateReservationAsync(start, start.AddDays(2));
        var stornoedStay = await scenario.CreateReservationAsync(
            start.AddDays(2),
            start.AddDays(4),
            roomId: scenario.SecondRoomAId);

        billedStay.TotalAmount.Should().Be(StayGross);
        stornoedStay.TotalAmount.Should().Be(StayGross);

        // (1) Normal akis: fatura kesinlesir ve oyle kalir.
        var billedInvoice = await scenario.CreateReservationInvoiceAsync(billedStay.Id);
        await scenario.FinalizeInvoiceAsync(billedInvoice.Id);

        // (2) Hatanin akisi: fatura kesinlesir, sonra iptal edilir (storno uretilir).
        var stornoedInvoice = await scenario.CreateReservationInvoiceAsync(stornoedStay.Id);
        await scenario.FinalizeInvoiceAsync(stornoedInvoice.Id);
        var afterCancel = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest
        {
            Id = stornoedInvoice.Id,
            Reason = "Yanlis misafire kesildi."
        });

        afterCancel.CancelledByInvoiceId.Should().NotBeNull("iptal, kesinlesmis belgede storno uretir");

        // Rapor kapsami HotelReader.AccessibleHotels() ile cozulur; dispatcher seviyesindeki
        // kimlik icin UserHotelAccess satiri yoktur, bu yuzden Head Office kullanicisi taklit
        // edilir. Kapsam yine de TEK OTELE daralir (aktif otel = A), asagidaki scope iddiasi
        // bunu kilitler.
        scenario.Host.CurrentUser.CanAccessAllHotels = true;

        // --- Yalnizca faturalanmis konaklamanin penceresi -----------------------------------
        var billedWindow = await GetRevenueAsync(scenario, start, start.AddDays(1));

        billedWindow.Scope.Mode.Should().Be(ReportScopeModes.Hotel);
        billedWindow.Scope.HotelId.Should().Be(scenario.HotelAId);
        billedWindow.SoldRoomNights.Should().Be(2);
        billedWindow.RoomRevenue.Gross.Should().Be(StayGross);
        billedWindow.TotalRevenue.Gross.Should().Be(StayGross);
        billedWindow.CityTaxCollected.Should().Be(StayCityTax, "Kurtaxe tahsil edildi ama ciro degildir");
        billedWindow.UnbilledRoomRevenueGross.Should().Be(
            0m,
            "yururlukteki bir belgesi olan konaklama faturalanmis sayilir");

        // --- Yalnizca storno'lu konaklamanin penceresi ---------------------------------------
        var stornoedWindow = await GetRevenueAsync(scenario, start.AddDays(2), start.AddDays(3));

        stornoedWindow.SoldRoomNights.Should().Be(2, "rezervasyon iptal edilmedi, yalnizca fatura");
        stornoedWindow.RoomRevenue.Net.Should().Be(0m);
        stornoedWindow.RoomRevenue.Vat.Should().Be(0m);
        stornoedWindow.TotalRevenue.Gross.Should().Be(
            0m,
            "orijinal (+X) ile storno (−X) birlikte sayilir ve tam sifir eder");
        stornoedWindow.CityTaxCollected.Should().Be(0m, "Kurtaxe satiri da storno'da negatiflenir");
        stornoedWindow.UnbilledRoomRevenueGross.Should().Be(
            stornoedStay.TotalAmount,
            "iptal edilmis faturanin konaklamasi yeniden 'faturalanmamis' olur");

        // --- Iki konaklamayi da kapsayan pencere: RAPORUN ASIL GARANTISI ---------------------
        var wholeWindow = await GetRevenueAsync(scenario, start, start.AddDays(3));

        wholeWindow.SoldRoomNights.Should().Be(4);
        wholeWindow.ExtraRevenue.Gross.Should().Be(0m, "degismezin gecerlilik kosulu: ekstra yok");

        var stayValue = billedStay.TotalAmount + stornoedStay.TotalAmount;

        (wholeWindow.TotalRevenue.Gross + wholeWindow.UnbilledRoomRevenueGross).Should().Be(
            stayValue,
            "raporun iki tarafi birbirini tamamlar: hicbir tutar iki kova arasinda kaybolmaz");

        // Degismezin DAYANIKLI hâli: <c>unbilled</c> yalnizca KONAKLAMA tutarini olctugu icin
        // karsiligi <c>roomRevenue</c>'dur. Ekstra iceren bir donemde ust satirdaki esitlik
        // ekstra kadar sapar, bu satirdaki sapmaz — regresyon agi asil buraya gerilir.
        (wholeWindow.RoomRevenue.Gross + wholeWindow.UnbilledRoomRevenueGross).Should().Be(
            stayValue,
            "konaklama geliri + faturalanmamis konaklama = konaklamalarin toplam degeri");
    }

    /// <summary>
    /// Taslakken iptal edilen fatura konaklamayi <b>faturalanmis</b> yapmaz: taslak numara
    /// almamistir, yani hic belge olmamistir. Tutar bastan sona <c>unbilled</c> tarafinda kalir —
    /// degismezin diger ucu.
    /// </summary>
    [RequiresPostgresFact]
    public async Task A_stay_whose_draft_was_cancelled_stays_fully_unbilled()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var start = scenario.Today.AddDays(20);
        var stay = await scenario.CreateReservationAsync(start, start.AddDays(2));

        var draft = await scenario.CreateReservationInvoiceAsync(stay.Id);
        draft.InvoiceNumber.Should().BeNull("taslak numara almaz");

        await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest { Id = draft.Id });

        scenario.Host.CurrentUser.CanAccessAllHotels = true;

        var report = await GetRevenueAsync(scenario, start, start.AddDays(1));

        report.TotalRevenue.Gross.Should().Be(0m);
        report.UnbilledRoomRevenueGross.Should().Be(stay.TotalAmount);

        (report.TotalRevenue.Gross + report.UnbilledRoomRevenueGross).Should().Be(stay.TotalAmount);
    }

    /// <summary>
    /// Iptal edilen <b>rezervasyon</b> raporun konaklama tarafinda hic yer almaz
    /// (<c>Cancelled</c>/<c>NoShow</c> odayi bloke etmez): ne satilan oda-gece ne de
    /// <c>unbilled</c> uretir. Degismezin toplaminda da bulunmamalidir.
    /// </summary>
    [RequiresPostgresFact]
    public async Task A_cancelled_reservation_is_outside_both_buckets()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var start = scenario.Today.AddDays(30);
        var stay = await scenario.CreateReservationAsync(start, start.AddDays(2));

        await using (var database = fixture.CreateDbContext())
        {
            var tracked = await database.Reservations.IgnoreQueryFilters()
                .FirstAsync(candidate => candidate.Id == stay.Id);
            tracked.Status = ReservationStatus.Cancelled;
            await database.SaveChangesAsync();
        }

        scenario.Host.CurrentUser.CanAccessAllHotels = true;

        var report = await GetRevenueAsync(scenario, start, start.AddDays(1));

        report.SoldRoomNights.Should().Be(0);
        report.TotalRevenue.Gross.Should().Be(0m);
        report.UnbilledRoomRevenueGross.Should().Be(0m);
    }

    private static Task<RevenueReportResponse> GetRevenueAsync(
        BookingScenario scenario,
        DateOnly from,
        DateOnly to) =>
        scenario.Host.Dispatcher.Send(new GetRevenueReportRequest { From = from, To = to });
}
