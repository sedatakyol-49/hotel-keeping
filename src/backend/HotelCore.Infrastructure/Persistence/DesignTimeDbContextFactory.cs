using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HotelCore.Infrastructure.Persistence;

/// <summary>
/// <c>dotnet ef</c> araçlarının (migration üretimi / database update) DbContext'i uygulamayı
/// ayağa kaldırmadan oluşturabilmesi için design-time fabrika.
/// <para>
/// Connection string okuma sırası (ilk bulunan kazanır):
/// <list type="number">
///   <item><description><c>ConnectionStrings__Default</c> ortam değişkeni (CI ve container senaryoları).</description></item>
///   <item><description>Api projesinin user-secrets deposu (<c>UserSecretsId = hotelcore-api</c>,
///   anahtar <c>ConnectionStrings:Default</c>) — geliştiricinin yerel kimlik bilgileri.</description></item>
///   <item><description>Yalnızca model karşılaştırması için yeterli olan YER TUTUCU.</description></item>
/// </list>
/// Hiçbir gerçek kimlik bilgisi bu dosyada TUTULMAZ.
/// </para>
/// <para>
/// Kimlik bağlamı (ICurrentUser) design-time'da mevcut değildir; DbContext bunu opsiyonel aldığı
/// için migration üretimi kimlikten bağımsız çalışır.
/// </para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>Api projesinin csproj'undaki <c>UserSecretsId</c> ile aynı olmalıdır.</summary>
    private const string ApiUserSecretsId = "hotelcore-api";

    private const string ConnectionStringName = "Default";

    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__Default";

    private const string DesignTimePlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=hotelcore_designtime;Username=postgres;Password=set-via-user-secrets";

    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ResolveConnectionString(), npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        // AddUserSecrets(string) overload'ı zaten optional'dır: secrets.json yoksa sessizce atlanır.
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .Build();

        var fromUserSecrets = configuration.GetConnectionString(ConnectionStringName);

        return string.IsNullOrWhiteSpace(fromUserSecrets)
            ? DesignTimePlaceholderConnectionString
            : fromUserSecrets;
    }
}
