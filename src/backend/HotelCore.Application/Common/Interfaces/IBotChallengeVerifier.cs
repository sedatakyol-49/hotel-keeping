namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Bot doğrulaması (Turnstile / hCaptcha / reCAPTCHA benzeri) soyutlaması.
/// <para>
/// <b>Neden soyutlama, doğrudan bir sağlayıcı değil:</b> rezervasyon ucu envanteri park eden
/// betiklere karşı korunmalıdır, ama sağlayıcı seçimi müşteri/veri koruma kararıdır (üçüncü
/// tarafa IP aktarımı). Sağlayıcı takıldığında değişecek tek yer bu arayüzün implementasyonudur;
/// istek şeması (<c>challengeToken</c>) zaten sözleşmede yerini almıştır.
/// </para>
/// <para>
/// Bu fazda kayıtlı implementasyon <c>NullBotChallengeVerifier</c>'dır: <b>doğrulama yapmaz</b>,
/// yalnızca kararı loglar. Kötüye kullanım savunması bu fazda hız sınırına dayanır.
/// </para>
/// </summary>
public interface IBotChallengeVerifier
{
    /// <summary>Doğrulama gerçekten yapılıyor mu (yapılandırılmamışsa <c>false</c>).</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Belirteci doğrular. <c>false</c> dönerse istek <c>400</c> ile reddedilir.
    /// </summary>
    /// <param name="challengeToken">İstemcinin gönderdiği opak belirteç (bu fazda <c>null</c>).</param>
    /// <param name="clientIp">İstemci IP'si (sağlayıcıya iletilecek); bilinmiyorsa <c>null</c>.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task<bool> VerifyAsync(string? challengeToken, string? clientIp, CancellationToken cancellationToken);
}
