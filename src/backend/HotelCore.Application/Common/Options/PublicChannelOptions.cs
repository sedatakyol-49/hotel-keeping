namespace HotelCore.Application.Common.Options;

/// <summary>
/// Misafir kanalının çalışma zamanı ayarları. <b>Koda gömülmez</b>; Api katmanı bunu
/// <c>appsettings</c> → <c>PublicChannel</c> bölümünden doldurur.
/// <para>
/// <b>Neden düz bir POCO:</b> Application katmanı <c>IConfiguration</c>'a bağımlı değildir
/// (LayerDependencyTests). Ayarların <i>kaynağı</i> host'un kararıdır, <i>anlamı</i> use-case'in.
/// </para>
/// </summary>
public sealed class PublicChannelOptions
{
    /// <summary>Yapılandırma bölümü adı.</summary>
    public const string SectionName = "PublicChannel";

    /// <summary>
    /// İstemci IP özetinin tuzu. <b>Ham IP saklanmaz</b>; tuz yapılandırmadan geldiği için özet
    /// başka veri kümeleriyle eşleştirilemez (rainbow table ile IP geri çözülemez).
    /// Boş bırakılırsa IP hiç özetlenmez ve <c>ClientIpHash</c> <c>null</c> kalır.
    /// </summary>
    public string ClientIpHashSalt { get; set; } = string.Empty;

    /// <summary>
    /// <c>POST /bookings/lookup</c> ucunun <b>sabit</b> minimum yanıt süresi (ms).
    /// <para>
    /// <b>Neden:</b> uç ne gövdede ne de <i>zamanlamada</i> bir rezervasyonun varlığını
    /// sızdırmalıdır. Eşleşme bulunduğunda e-posta kuyruğa alınır, bulunmadığında hiçbir şey
    /// yapılmaz; iki yolun süreleri farklı olursa saldırgan referans/e-posta çiftlerini
    /// doğrulayabilir.
    /// </para>
    /// </summary>
    public int LookupMinimumResponseMilliseconds { get; set; } = 400;

    /// <summary>Onay belgesi şablonunun versiyonu (<c>PublicBooking.ConfirmationDocumentVersion</c>).</summary>
    public string ConfirmationDocumentVersion { get; set; } = "1";

    /// <summary>
    /// Onay e-postasındaki erişim bağlantısının şablonu. Yer tutucular:
    /// <c>{culture}</c>, <c>{hotelSlug}</c>, <c>{accessToken}</c>.
    /// </summary>
    public string AccessLinkTemplate { get; set; } =
        "https://localhost:4200/{culture}/{hotelSlug}/buchung/{accessToken}";

    /// <summary>Erişim token'ının geçerlilik süresi: <c>checkOut</c> + bu kadar gün.</summary>
    public int AccessTokenValidityDaysAfterCheckOut { get; set; } = 30;
}
