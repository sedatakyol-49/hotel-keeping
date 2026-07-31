namespace HotelCore.Api.Startup;

/// <summary>
/// Public kanalın <b>HTTP tarafındaki</b> ayarları (<c>appsettings</c> → <c>PublicChannel</c>).
/// Use-case tarafındaki ayarlar <c>PublicChannelOptions</c>'tadır; ikisi aynı bölümü okur.
/// </summary>
public sealed class PublicChannelSettings
{
    /// <summary>Yapılandırma bölümü adı.</summary>
    public const string SectionName = "PublicChannel";

    /// <summary>
    /// Güvenilen ters proxy adresleri. <c>X-Forwarded-For</c> <b>yalnızca</b> bu listedeki bir
    /// adresten geldiğinde okunur.
    /// <para>
    /// <b>Neden liste zorunlu:</b> header'ı koşulsuz okumak, hız sınırını istemcinin
    /// <i>kendi beyanına</i> bağlar — saldırgan her istekte farklı bir IP uydurup sınırı sonsuza
    /// kadar sıfırlayabilirdi. Liste boşsa header hiç okunmaz ve soket adresi kullanılır.
    /// </para>
    /// </summary>
    public IList<string> TrustedProxies { get; } = [];

    /// <summary>
    /// Uç bazında hız sınırları: <c>kural adı → (adet, pencere)</c>. Değerler
    /// <b>koda gömülmez</b> (api-contracts-public-booking.md §1.2); burada tanımlı olmayan bir
    /// kural için sınır uygulanmaz.
    /// </summary>
    public IDictionary<string, PublicRateLimitRule> RateLimits { get; } =
        new Dictionary<string, PublicRateLimitRule>(StringComparer.Ordinal);
}

/// <summary>Tek bir hız sınırı kuralı: pencere başına izin verilen istek sayısı.</summary>
public sealed class PublicRateLimitRule
{
    /// <summary>Pencere içinde izin verilen istek sayısı.</summary>
    public int PermitLimit { get; set; }

    /// <summary>Pencere uzunluğu (saniye).</summary>
    public int WindowSeconds { get; set; } = 60;
}
