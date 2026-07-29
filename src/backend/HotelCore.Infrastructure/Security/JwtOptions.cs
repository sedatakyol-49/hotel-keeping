namespace HotelCore.Infrastructure.Security;

/// <summary>
/// <c>appsettings.json &gt; "Jwt"</c> bölümünün karşılığı.
/// <para>
/// <b>Secret koda veya appsettings.json'a YAZILMAZ.</b> Development'ta
/// <c>dotnet user-secrets set "Jwt:Secret" ...</c>, diğer ortamlarda <c>Jwt__Secret</c>
/// ortam değişkeni ile verilir. Eksik/kısa olması durumunda uygulama açıklayıcı bir
/// hata ile başlamayı reddeder (sessizce zayıf anahtara düşmez).
/// </para>
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 için gereken en az anahtar uzunluğu (256 bit = 32 karakter).</summary>
    public const int MinimumSecretLength = 32;

    public string Issuer { get; set; } = "HotelCore";

    public string Audience { get; set; } = "HotelCore.Api";

    /// <summary>HMAC-SHA256 imza anahtarı. Yapılandırmadan gelir; asla commit edilmez.</summary>
    public string Secret { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>
    /// Yapılandırmayı doğrular. Geçersizse <see cref="InvalidOperationException"/> fırlatır.
    /// Hem Api (token doğrulama parametreleri) hem Infrastructure (token üretimi) çağırır.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Secret))
        {
            throw new InvalidOperationException(
                "JWT imza anahtari bulunamadi. 'Jwt:Secret' degerini dotnet user-secrets " +
                "(Development) veya Jwt__Secret ortam degiskeni ile saglayin. " +
                "Bu deger kaynak koda veya appsettings.json'a YAZILMAZ.");
        }

        if (Secret.Length < MinimumSecretLength)
        {
            throw new InvalidOperationException(
                $"'Jwt:Secret' cok kisa ({Secret.Length} karakter). HS256 icin en az " +
                $"{MinimumSecretLength} karakter gereklidir.");
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("'Jwt:Issuer' ve 'Jwt:Audience' bos olamaz.");
        }

        if (AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("'Jwt:AccessTokenMinutes' pozitif olmalidir.");
        }

        if (RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("'Jwt:RefreshTokenDays' pozitif olmalidir.");
        }
    }
}
