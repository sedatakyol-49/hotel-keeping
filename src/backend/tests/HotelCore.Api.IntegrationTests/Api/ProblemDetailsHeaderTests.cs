using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Api;

/// <summary>
/// Hata yanitlarinin <b>basliklarinin</b> sozlesmeye uydugunu kilitler (api-contracts.md).
///
/// <para><b>Kilitlenen iki hata:</b>
/// <list type="number">
///   <item><c>Content-Language</c> hata yanitlarinda <b>hic yoktu</b>: localization middleware
///   basligi yazar, ama bir istisna yukari kabardiginda <c>ExceptionHandlerMiddleware</c> yaniti
///   <c>Response.Clear()</c> ile sifirlar ve yazilmis tum basliklar silinir. Yani baslik
///   <b>en cok gerektigi yerde</b> yoktu: istemci hangi dilde bir <c>detail</c> aldigini
///   bilemiyordu.</item>
///   <item><c>application/problem+json</c> <b>charset'siz</b> gidiyordu: govdeyi yazan
///   <c>DefaultProblemDetailsWriter</c> <c>Content-Type</c>'i en son yazar ve onceden konan
///   degeri ezer. Almanca/Turkce metinler JSON kacisi yapilmadan tasindigi icin bu, istemcide
///   mojibake riskidir.</item>
/// </list></para>
///
/// <para><b>Neden mesaj metnine bakilmiyor:</b> yanit govdesindeki <c>title</c>/<c>detail</c>
/// yerellestirilmistir; iddialar yalnizca <b>basliklara</b> ve durum koduna bakar. Dil secimi
/// <c>Accept-Language</c> ile yapilir — kultur cozum sirasinda bu saglayici JWT'deki
/// <c>culture</c> claim'inden <b>oncedir</b>, yani token "de" tasisa da istenen dil kazanir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProblemDetailsHeaderTests(PostgresFixture fixture)
{
    private const string ProblemJsonWithCharset = "application/problem+json; charset=utf-8";

    private static readonly string[] FrontOfficePermissions =
    [
        Permissions.ReservationsView,
        Permissions.ReservationsCreate
    ];

    private static Uri ReservationsUri { get; } = new("api/v1/reservations", UriKind.Relative);

    [RequiresPostgresTheory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("tr")]
    public async Task A_validation_problem_declares_the_requested_language_and_utf8(string language)
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = CreateClient(scenario, language);
        var start = scenario.Today.AddDays(10);

        // Kapasite asimi (oda tipi kapasitesi 4) -> ValidationException -> 400.
        using var response = await client.PostAsJsonAsync(
            ReservationsUri,
            Body(scenario, start, start.AddDays(2), adults: 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertProblemHeaders(response, language);
    }

    [RequiresPostgresTheory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("tr")]
    public async Task A_conflict_problem_declares_the_requested_language_and_utf8(string language)
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = CreateClient(scenario, language);
        var start = scenario.Today.AddDays(10);

        await scenario.CreateReservationAsync(start, start.AddDays(3));

        // Ayni odada cakisan tarih -> ConflictException -> 409.
        using var response = await client.PostAsJsonAsync(
            ReservationsUri,
            Body(scenario, start.AddDays(1), start.AddDays(2)));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        AssertProblemHeaders(response, language);
    }

    /// <summary>
    /// Basarili yanit da ayni dili bildirmelidir; aksi hâlde "hata yanitinda dil var" iddiasi
    /// tek basina bir sey soylemezdi (istemci ve onbellek dili her yanitta ayni yerden okur).
    /// </summary>
    [RequiresPostgresTheory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("tr")]
    public async Task A_successful_response_declares_the_same_language(string language)
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = CreateClient(scenario, language);

        using var response = await client.GetAsync(ReservationsUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLanguage.Should().ContainSingle()
            .Which.Should().Be(language);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    private static void AssertProblemHeaders(HttpResponseMessage response, string language)
    {
        response.Content.Headers.ContentLanguage.Should().ContainSingle(
            "istemci hangi dilde bir 'detail' aldigini basliktan okuyabilmelidir")
            .Which.Should().Be(language);

        response.Content.Headers.ContentType?.ToString().Should().Be(
            ProblemJsonWithCharset,
            "RFC 7807 govdesi UTF-8 tasir; charset'i bildirmemek mojibake riskidir");
    }

    private static HttpClient CreateClient(BookingScenario scenario, string language)
    {
        var client = scenario.CreateClient(FrontOfficePermissions);
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

        return client;
    }

    private static object Body(
        BookingScenario scenario,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults = 2) => new
        {
            roomId = scenario.RoomAId,
            guestId = scenario.GuestAId,
            checkIn = checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            checkOut = checkOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            adults,
            channel = nameof(ReservationChannel.Direct)
        };
}
