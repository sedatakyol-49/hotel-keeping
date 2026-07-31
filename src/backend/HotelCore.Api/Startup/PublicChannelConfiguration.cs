using HotelCore.Api.Services;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Options;

namespace HotelCore.Api.Startup;

/// <summary>
/// Misafire açık kanalın DI kompozisyonu.
///
/// <para><b>Soyutlamalar Application'da, taşıyıcılar burada.</b> Bu fazda kayıtlı olan
/// implementasyonların hepsi <i>geliştirme</i> uygulamalarıdır ve bu <b>bilinçlidir</b>: gerçek
/// bir PSP, bot sağlayıcısı veya e-posta taşıyıcısı seçmek altyapı <b>ve veri koruma</b>
/// kararıdır. Sözleşme, akış ve zorunlu içerik bugün sabitlenir; taşıyıcı sonra takılır ve
/// değişen tek yer bu dosya olur.</para>
/// </summary>
public static class PublicChannelConfiguration
{
    /// <summary>Public kanalın ayarlarını, portlarını ve arka plan servislerini kaydeder.</summary>
    public static IServiceCollection AddPublicChannel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(PublicChannelSettings.SectionName);

        services.Configure<PublicChannelSettings>(section);

        // Use-case tarafındaki ayarlar düz bir POCO olarak verilir: Application katmanı
        // IConfiguration'a bağımlı değildir (LayerDependencyTests).
        var options = section.Get<PublicChannelOptions>() ?? new PublicChannelOptions();
        services.AddSingleton(options);

        // Hız sınırı ve süpürücü gerçek zamana bağlıdır ve singleton'dır; scoped saat portunu
        // enjekte etmek captive dependency olurdu.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IPublicRateLimiter, PublicRateLimiter>();
        services.AddScoped<PublicClientAddress>();

        // --- Geliştirme uygulamaları -----------------------------------------------------------
        // NullPaymentAuthorizationProvider: SupportsGuarantee = false → garanti istenirse
        // 400 CHANNEL_NOT_CONFIGURED. Kart verisi bu yoldan da geçmez.
        services.AddSingleton<IPaymentAuthorizationProvider, NullPaymentAuthorizationProvider>();

        // NullBotChallengeVerifier: doğrulama YAPMAZ. Kötüye kullanım savunması bu fazda hız
        // sınırına dayanır; IsEnabled = false bunu gözlemlenebilir kılar.
        services.AddSingleton<IBotChallengeVerifier, NullBotChallengeVerifier>();

        // §312f onayı: outbox + taşıyıcı. Gönderim rezervasyonla AYNI transaction'da yapılmaz.
        services.AddSingleton<BookingConfirmationOutbox>();
        services.AddSingleton<IBookingConfirmationOutbox>(provider =>
            provider.GetRequiredService<BookingConfirmationOutbox>());
        services.AddScoped<IBookingConfirmationSender, LoggingBookingConfirmationSender>();
        services.AddHostedService<BookingConfirmationDispatcher>();

        // Süresi dolmuş hold'ların fiziksel süpürücüsü (çakışma kısıtı zaman ifadesi içeremez).
        services.AddHostedService<PublicHoldSweeper>();

        return services;
    }
}
