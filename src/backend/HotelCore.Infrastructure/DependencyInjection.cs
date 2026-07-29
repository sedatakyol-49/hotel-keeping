using HotelCore.Application.Common.Interfaces;
using HotelCore.Infrastructure.Persistence;
using HotelCore.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Infrastructure;

/// <summary>Infrastructure katmanının DI kaydı.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// PostgreSQL DbContext'ini ve güvenlik servislerini (parola özetleme, JWT üretimi) kaydeder.
    /// <c>ICurrentUser</c> ve <c>IDateTimeProvider</c> implementasyonları Api katmanının
    /// sorumluluğundadır; kayıtlıysa DbContext bunları otomatik alır (kayıtlı değilse
    /// kimliksiz/güvenli varsayılanla çalışır).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Gerçek connection string repoda TUTULMAZ: user-secrets veya
            // ConnectionStrings__Default ortam değişkeni ile verilir.
            throw new InvalidOperationException(
                "Veritabani baglanti dizesi bulunamadi. 'ConnectionStrings:Default' degerini " +
                "dotnet user-secrets veya ConnectionStrings__Default ortam degiskeni ile saglayin.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        AddSecurity(services, configuration);

        return services;
    }

    /// <summary>
    /// Güvenlik altyapısı: BCrypt parola özetleme + JWT/refresh token üretimi.
    /// Secret yapılandırmadan (user-secrets / ortam değişkeni) okunur; koda yazılmaz.
    /// </summary>
    private static void AddSecurity(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // PasswordHasher durumsuzdur → singleton.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // JwtTokenService scoped'dır: scoped kaydedilen IDateTimeProvider'a bağımlıdır
        // (singleton olsaydı captive dependency oluşurdu).
        services.AddScoped<IJwtTokenService, JwtTokenService>();
    }
}
