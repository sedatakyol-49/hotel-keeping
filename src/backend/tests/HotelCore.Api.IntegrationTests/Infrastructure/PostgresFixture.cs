using Testcontainers.PostgreSql;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test kosusu boyunca tek bir PostgreSQL ornegi saglar.
/// CI'da service container'a baglanir; yerelde Testcontainers ile gecici konteyner baslatir.
/// Kaynak yoksa <see cref="ConnectionString"/> bos kalir ve testler
/// <see cref="RequiresPostgresFactAttribute"/> sayesinde zaten atlanmis olur.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (DatabaseAvailability.ExternalConnectionString is { } external)
        {
            ConnectionString = external;
            return;
        }

        if (!DatabaseAvailability.IsDockerAvailable)
        {
            return;
        }

        // Uygulamanin hedefledigi PostgreSQL 16 ile ayni ana surum.
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("hotelcore_it")
            .WithUsername("postgres")
            // Gecici konteyner sifresi — gercek secret DEGILDIR, konteynerle birlikte yok olur.
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
