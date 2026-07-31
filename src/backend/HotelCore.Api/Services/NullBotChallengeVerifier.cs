using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Api.Services;

/// <summary>
/// Geliştirme/varsayılan bot doğrulayıcı: <b>doğrulama yapmaz</b>, her belirteci kabul eder.
///
/// <para><b>Neden yine de bir implementasyon var:</b> <c>challengeToken</c> alanı sözleşmede
/// bugünden yerini alır ve rezervasyon handler'ı doğrulama adımını bugünden çağırır. Sağlayıcı
/// takıldığında değişen tek şey bu tipin yerine gelen implementasyondur — istek şeması, hata
/// kodu ve akış aynı kalır.</para>
///
/// <para><b>Bu fazda kötüye kullanım savunması hız sınırına dayanır</b> (uç bazında IP + e-posta
/// özeti). <see cref="IsEnabled"/> <c>false</c> döndüğü için bu durum gözlemlenebilirdir; sessiz
/// bir "her şey yolunda" izlenimi verilmez.</para>
/// </summary>
public sealed class NullBotChallengeVerifier : IBotChallengeVerifier
{
    public bool IsEnabled => false;

    public Task<bool> VerifyAsync(
        string? challengeToken,
        string? clientIp,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
