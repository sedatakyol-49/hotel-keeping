using HotelCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test kosusu boyunca tek bir PostgreSQL ornegi saglar.
/// CI'da service container'a baglanir; yerelde Testcontainers ile gecici konteyner baslatir.
/// Kaynak yoksa <see cref="ConnectionString"/> bos kalir ve testler
/// <see cref="RequiresPostgresFactAttribute"/> sayesinde zaten atlanmis olur.
/// <para>
/// Ayrica API host'unu (<see cref="Api"/>) ve migration'lari <b>koleksiyon basina bir kez</b>
/// paylasir: <c>PostgresCollection</c> icindeki tum test siniflari sirayla kostugu icin bu
/// guvenlidir ve her test sinifinda yeni bir TestServer ayaga kaldirma maliyetini ortadan kaldirir.
/// </para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private HotelCoreApiFactory? _api;
    private bool _migrated;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Paylasilan API host'u (bellek ici TestServer). Ilk erisimde kurulur.</summary>
    internal HotelCoreApiFactory Api => _api ??= new HotelCoreApiFactory(ConnectionString);

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

    /// <summary>
    /// Semayi hazir hale getirir (idempotent). API host'u "Testing" ortaminda migration
    /// UYGULAMAZ (bu yalnizca Development'ta yapilir), bu yuzden testler semadan kendileri
    /// sorumludur.
    /// <para>
    /// Kilit gerekmez: <c>PostgresCollection</c> icindeki tum siniflar <b>sirayla</b> kosar
    /// (xUnit ayni koleksiyonu paralel yurutmez) ve <c>MigrateAsync</c> zaten idempotenttir.
    /// </para>
    /// </summary>
    internal async Task EnsureMigratedAsync()
    {
        if (_migrated)
        {
            return;
        }

        await using var database = CreateDbContext();
        await database.Database.MigrateAsync();
        _migrated = true;
    }

    /// <summary>
    /// Test verisini kurmak/temizlemek icin kimlik baglami OLMAYAN bir DbContext uretir.
    /// Kimlik olmadigi icin tenant filtresi hicbir satiri gostermez; okuma yapilacaksa
    /// <c>IgnoreQueryFilters()</c> kullanilir.
    /// </summary>
    internal AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
