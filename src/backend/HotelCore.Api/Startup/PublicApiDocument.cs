namespace HotelCore.Api.Startup;

/// <summary>
/// İkinci OpenAPI belgesinin kimliği (architecture-public-booking.md §3).
///
/// <para><b>Neden belge ikiye ayrılıyor:</b> misafir uygulaması client'ını public belgeden üretir
/// ve admin şemalarının <b>tek bir tipini bile</b> görmemelidir. Tek bir belge, admin DTO'larını
/// (ve dolayısıyla iç alan adlarını, modül yapısını, izin kavramlarını) misafir paketine taşırdı —
/// bu hem bilgi sızıntısı hem de "yarın admin'e eklenen bir alan sessizce dışarı sızar" riskinin
/// kaynağıdır.</para>
///
/// <para>Ayrım <c>ApiExplorer</c> grup adıyla yapılır: public controller'lar
/// <c>[ApiExplorerSettings(GroupName = "public")]</c> taşır, admin belgesi bu grubu <b>dışlar</b>.
/// Yani yeni bir public uç eklemek için tek yapılması gereken grup adını vermektir; unutulursa
/// uç admin belgesine düşer ve DTO ayrıklığı testi kırılır.</para>
/// </summary>
public static class PublicApiDocument
{
    /// <summary>Public controller'ların <c>ApiExplorer</c> grup adı.</summary>
    public const string GroupName = "public";

    /// <summary>Public OpenAPI belgesinin adı (<c>/swagger/public-v1/swagger.json</c>).</summary>
    public const string DocumentName = "public-v1";

    /// <summary>Admin OpenAPI belgesinin adı (<c>/swagger/v1/swagger.json</c>).</summary>
    public const string AdminDocumentName = "v1";
}
