namespace HotelCore.Application.Features.Auth.Common;

/// <summary>
/// Kullanıcının erişebildiği otelin özeti (hotel switcher için).
/// Alan adları frontend sözleşmesiyle birebirdir.
/// </summary>
public sealed record HotelSummaryDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    /// <summary>ISO ülke kodu (enum'un metin karşılığı: "DE", "AT", "TR" ...).</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>ISO 4217 para birimi kodu.</summary>
    public string Currency { get; init; } = string.Empty;

    public string DefaultCulture { get; init; } = string.Empty;
}
