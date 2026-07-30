using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.Create;
using HotelCore.Application.Features.Invoices.Finalize;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Invoices;

/// <summary>
/// GoBD §6.2 — <b>bosluksuz (kesintisiz) fatura numarasi</b>.
/// <para>
/// Iki iddia ayri ayri kilitlenir:
/// <list type="number">
///   <item><b>Atlama yok:</b> ardisik finalize'lar 1, 2, 3... uretir.</item>
///   <item><b>Yarisi kaybeden numara TUKETMEZ:</b> eszamanli ikinci finalize 409 alir ve sayac
///         artmaz; istek tekrarlandiginda sekans kaldigi yerden devam eder.</item>
/// </list>
/// </para>
/// <para>
/// <b>Eszamanlilik nasil deterministik kuruldu:</b> is parcacigi yarisi yerine iki ayri
/// <b>uygulama grafigi</b> (ayri <c>AppDbContext</c> + ayri change tracker) kullanilir. Rakip
/// grafik sayaci once okur — bu andaki <c>Version</c> onun anlik goruntusudur —, ana grafik
/// finalize edip sayaci artirir, ardindan rakip grafik kendi finalize'ini calistirir. EF izlenen
/// satirin <i>orijinal</i> <c>Version</c> degerini <c>WHERE</c>'e koydugu icin UPDATE <b>0 satir</b>
/// etkiler ve <c>DbUpdateConcurrencyException</c> gercekten olusur. Adimlar testte elle
/// siralandigi icin zamanlama sansi yoktur → flaky degildir.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvoiceNumberSequenceTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Consecutive_finalizations_produce_a_gapless_sequence()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var first = await scenario.CreateFinalizedInvoiceAsync();
        var second = await scenario.CreateFinalizedInvoiceAsync();
        var third = await scenario.CreateFinalizedInvoiceAsync();

        first.InvoiceNumber.Should().Be(scenario.InvoiceNumber(1));
        second.InvoiceNumber.Should().Be(scenario.InvoiceNumber(2));
        third.InvoiceNumber.Should().Be(scenario.InvoiceNumber(3));

        (await scenario.ListInvoiceNumbersAsync()).Should().Equal(
            scenario.InvoiceNumber(1),
            scenario.InvoiceNumber(2),
            scenario.InvoiceNumber(3));
    }

    [RequiresPostgresFact]
    public async Task Finalizing_stamps_the_issue_date_and_locks_the_document()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        finalized.Status.Should().Be(nameof(InvoiceStatus.Finalized));
        finalized.IssuedAt.Should().Be(scenario.Clock.UtcNow);
    }

    [RequiresPostgresFact]
    public async Task A_draft_invoice_has_no_number_and_does_not_advance_the_counter()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        var draft = await scenario.CreateManualInvoiceAsync();

        draft.InvoiceNumber.Should().BeNull("numara yalnizca finalize aninda atanir");
        draft.IssuedAt.Should().BeNull();
        (await scenario.FindInvoiceCounterAsync())
            .Should().BeNull("taslak icin sayac satiri bile olusturulmaz");
    }

    [RequiresPostgresFact]
    public async Task Cancelling_a_draft_does_not_consume_a_number()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        var cancelled = await scenario.Host.Dispatcher.Send(new CancelInvoiceRequest
        {
            Id = draft.Id,
            Reason = "Yanlis misafir secildi."
        });

        cancelled.Status.Should().Be(nameof(InvoiceStatus.Cancelled));
        cancelled.InvoiceNumber.Should().BeNull();
        (await scenario.FindInvoiceCounterAsync()).Should().BeNull();

        // Iptalden SONRA kesilen ilk fatura yine 1 numarayi alir: taslak sekansi kirletmez.
        var next = await scenario.CreateFinalizedInvoiceAsync();
        next.InvoiceNumber.Should().Be(scenario.InvoiceNumber(1));
    }

    [RequiresPostgresFact]
    public async Task Finalizing_an_already_finalized_invoice_is_rejected_without_consuming_a_number()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var finalized = await scenario.CreateFinalizedInvoiceAsync();

        var act = async () => await scenario.FinalizeInvoiceAsync(finalized.Id);

        await act.Should().ThrowAsync<ConflictException>();

        (await scenario.FindInvoiceCounterAsync())!.LastNumber
            .Should().Be(1, "reddedilen finalize sayaci artirmaz");
    }

    [RequiresPostgresFact]
    public async Task An_invoice_without_line_items_cannot_be_finalized()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var draft = await scenario.CreateManualInvoiceAsync();

        // Satirlar dogrudan silinir (taslak oldugu icin GoBD guard'i engellemez).
        var database = scenario.Host.Database;
        var lines = await database.InvoiceLineItems.Where(line => line.InvoiceId == draft.Id).ToListAsync();
        database.InvoiceLineItems.RemoveRange(lines);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var act = async () => await scenario.FinalizeInvoiceAsync(draft.Id);

        await act.Should().ThrowAsync<ConflictException>();
        (await scenario.FindInvoiceCounterAsync()).Should().BeNull();
    }

    [RequiresPostgresFact]
    public async Task The_loser_of_a_concurrent_finalize_gets_a_conflict_and_leaves_no_gap()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        // Yilin ilk faturasi sayac SATIRINI olusturur. Yarisi sayac zaten varken kurmak
        // zorunludur: satir yokken iki taraf da INSERT denerdi ve gozlenen hata optimistic
        // concurrency degil benzersizlik ihlali olurdu (farkli bir savunma katmani).
        var warmup = await scenario.CreateFinalizedInvoiceAsync();
        warmup.InvoiceNumber.Should().Be(scenario.InvoiceNumber(1));

        var winner = await scenario.CreateManualInvoiceAsync();
        var loser = await scenario.CreateManualInvoiceAsync();

        var rival = scenario.CreateApplicationGraph();

        // (1) Rakip istek sayaci OKUR — bu andaki Version degeri onun anlik goruntusudur.
        var rivalCounter = await rival.Database.HotelInvoiceCounters
            .FirstAsync(counter => counter.HotelId == scenario.HotelAId && counter.Year == scenario.Year);
        rivalCounter.LastNumber.Should().Be(1);

        // (2) Kazanan istek sayaci artirir ve commit eder.
        var winnerResult = await scenario.FinalizeInvoiceAsync(winner.Id);
        winnerResult.InvoiceNumber.Should().Be(scenario.InvoiceNumber(2));

        // (3) Rakip istek simdi kendi finalize'ini calistirir. Elindeki sayac anlik goruntusu
        //     eskimistir; iki bagimsiz koruma ayni sonuca goturur:
        //       - sayac UPDATE'i "WHERE Version = eski" ile 0 satir etkiler (concurrency token),
        //       - ve/veya faturaya yazilmak istenen numara (HotelId, InvoiceNumber) unique
        //         index'i tarafindan reddedilir.
        //     Hangi ifadenin once calistigi EF'in toplu komut siralamasina baglidir, bu yuzden
        //     test MESAJA degil SONUCA bakar: 409 + numara tuketilmemis + sekansta bosluk yok.
        var act = async () => await rival.Dispatcher.Send(new FinalizeInvoiceRequest(loser.Id));

        await act.Should().ThrowAsync<ConflictException>();

        // Kaybeden fatura hala taslak; numarasi yok.
        var loserRow = await scenario.FindInvoiceAsync(loser.Id);
        loserRow!.Status.Should().Be(InvoiceStatus.Draft);
        loserRow.InvoiceNumber.Should().BeEmpty();

        // Sayac ilerlememis: transaction tamamen geri alindi.
        (await scenario.FindInvoiceCounterAsync())!.LastNumber
            .Should().Be(2, "kaybeden istek numara TUKETMEZ");

        // Istek tekrarlandiginda sekans kaldigi yerden devam eder: 1, 2, 3 — bosluk yok.
        var retry = await scenario.FinalizeInvoiceAsync(loser.Id);
        retry.InvoiceNumber.Should().Be(scenario.InvoiceNumber(3));

        (await scenario.ListInvoiceNumbersAsync()).Should().Equal(
            scenario.InvoiceNumber(1),
            scenario.InvoiceNumber(2),
            scenario.InvoiceNumber(3));
    }

    [RequiresPostgresFact]
    public async Task The_counter_row_itself_is_protected_by_an_optimistic_concurrency_token()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        await scenario.CreateFinalizedInvoiceAsync();

        // Yukaridaki uctan uca yarista 409'u hangi korumanin urettigi EF'in komut siralamasina
        // baglidir. Burada sayac satirinin KENDI korumasi yalitilmis olarak dogrulanir: iki ayri
        // change tracker ayni satiri okur, ilki commit eder, ikincisinin UPDATE'i 0 satir etkiler.
        var first = scenario.CreateApplicationGraph();
        var second = scenario.CreateApplicationGraph();

        var fromFirst = await first.Database.HotelInvoiceCounters
            .FirstAsync(counter => counter.HotelId == scenario.HotelAId && counter.Year == scenario.Year);
        var fromSecond = await second.Database.HotelInvoiceCounters
            .FirstAsync(counter => counter.HotelId == scenario.HotelAId && counter.Year == scenario.Year);

        fromFirst.LastNumber++;
        await first.Database.SaveChangesAsync();

        fromSecond.LastNumber++;
        var act = async () => await second.Database.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // Yarisi kaybeden hicbir sey yazmadi: sayac yalnizca BIR kez ilerledi.
        (await scenario.FindInvoiceCounterAsync())!.LastNumber.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task Each_hotel_keeps_its_own_sequence()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        await scenario.CreateFinalizedInvoiceAsync();

        // B otelinin baglami: numara sekansi otel + yil bazindadir, A oteli onu ilerletmez.
        var hotelB = scenario.CreateApplicationGraph(activeHotelId: scenario.HotelBId);
        var draftInB = await hotelB.Dispatcher.Send(new CreateInvoiceRequest
        {
            GuestId = scenario.GuestBId,
            LineItems = [BookingScenario.Line(InvoiceLineType.Extra, "Minibar", 1m, 10m)]
        });
        var finalizedInB = await hotelB.Dispatcher.Send(new FinalizeInvoiceRequest(draftInB.Id));

        finalizedInB.InvoiceNumber.Should().Be(scenario.InvoiceNumber(1));
        (await scenario.ListInvoiceNumbersAsync(scenario.HotelBId)).Should().ContainSingle();
    }
}
