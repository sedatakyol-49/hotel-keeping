using System.Net;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests;

/// <summary>
/// API host'unun uctan uca ayaga kalktigini dogrulayan smoke testler:
/// DI grafigi kurulabiliyor mu, middleware boru hatti calisiyor mu, hatalar
/// RFC 7807 ProblemDetails olarak donuyor mu.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApiSmokeTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Host_starts_and_builds_the_service_provider()
    {
        await using var factory = new HotelCoreApiFactory(fixture.ConnectionString);

        // CreateClient() host'u gercekten baslatir; DI kaydi bozuksa burada patlar.
        using var client = factory.CreateClient();

        client.BaseAddress.Should().NotBeNull();
        factory.Services.Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Unknown_route_returns_problem_details_instead_of_an_unhandled_error()
    {
        await using var factory = new HotelCoreApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("hotelcore-unknown-route", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // UseStatusCodePages + AddProblemDetails: govde application/problem+json olmalidir.
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
