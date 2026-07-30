namespace HotelCore.Application.Features.HeadOffices.Common;

/// <summary>
/// Head Office (marka) ayarları — api-contracts.md → "Hotels &amp; Ayarlar".
/// <para>
/// Marka adı <b>koda hardcode edilmez</b>: müşteriye görünen ad buradan yönetilir
/// (README → "HotelCore yalnızca kod/repo seviyesi isimdir").
/// </para>
/// </summary>
public sealed record HeadOfficeSettingsResponse
{
    public Guid Id { get; init; }

    public string BrandName { get; init; } = string.Empty;

    /// <summary>Yeni otellerin ve kullanıcıların varsayılan arayüz dili.</summary>
    public string DefaultCulture { get; init; } = string.Empty;

    /// <summary>Bu Head Office'e bağlı silinmemiş otel sayısı.</summary>
    public int HotelCount { get; init; }
}
