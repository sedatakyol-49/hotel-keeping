using HotelCore.Infrastructure.Persistence;
using HotelCore.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.Startup;

/// <summary>
/// Development'ta uygulama açılırken bekleyen migration'ları uygular ve seed'i çalıştırır.
/// <para>
/// <b>Production'da çağrılmaz:</b> orada şema değişikliği ayrı ve denetlenen bir dağıtım
/// adımıdır (<c>dotnet ef database update</c> / migration bundle). Otomatik migrate,
/// çok örnekli (scale-out) dağıtımda yarış koşulu yaratır.
/// </para>
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDevelopmentAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseInitializer));

        logger.DatabaseInitializing();

        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        // includeDevelopmentData: demo otel/oda/departman ve demo admin kullanicisi.
        await DbSeeder.SeedAsync(context, includeDevelopmentData: true, cancellationToken).ConfigureAwait(false);

        logger.DatabaseReady();
    }
}
