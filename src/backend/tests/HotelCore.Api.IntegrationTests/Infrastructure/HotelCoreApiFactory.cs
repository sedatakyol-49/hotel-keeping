using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// API host'unu bellek ici TestServer olarak ayaga kaldirir.
/// Gercek yapilandirma dosyalari kullanilir; yalnizca secret niteligindeki degerler
/// (connection string, JWT secret) test degerleriyle EZILIR — repoda gercek secret yoktur.
/// </summary>
internal sealed class HotelCoreApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Yalnizca test icin uretilmis, hicbir ortamda kullanilmayan imza anahtari.</summary>
    private const string TestJwtSecret = "hotelcore-integration-test-signing-key-not-a-real-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
    }
}
