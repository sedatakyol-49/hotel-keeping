namespace HotelCore.Application.Features.Hotels.Common;

/// <summary>
/// Otelin vergi profili — api-contracts.md → "Hotels &amp; Ayarlar".
/// <para>
/// Oranlar <b>koda hardcode edilmez</b> (architecture.md §4.1); otel bazında yönetilir ve
/// faturalama bu değerleri okur.
/// </para>
/// </summary>
public sealed record TaxProfileDto
{
    /// <summary>Standart KDV oranı, yüzde (DE: 19).</summary>
    public decimal VatRate { get; init; }

    /// <summary>İndirimli KDV oranı, yüzde (DE: 7 — konaklama).</summary>
    public decimal ReducedVatRate { get; init; }

    /// <summary>Kurtaxe: kişi başı / gece şehir vergisi tutarı.</summary>
    public decimal CityTaxPerPersonNight { get; init; }

    public bool CityTaxEnabled { get; init; }
}

/// <summary>Otel listesi satırı — otel seçici ve ayarlar listesi için yeterli alanlar.</summary>
public sealed record HotelListItemResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    /// <summary>Ülke enum <b>adı</b> (string: <c>DE | AT | CH | TR</c>), sayı değil.</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>ISO 4217 para birimi kodu.</summary>
    public string Currency { get; init; } = string.Empty;

    public string DefaultCulture { get; init; } = string.Empty;

    /// <summary>Otelin silinmemiş oda sayısı.</summary>
    public int RoomCount { get; init; }
}

/// <summary>Otel detayı — api-contracts.md → "Hotels &amp; Ayarlar" ile birebir.</summary>
public sealed record HotelResponse
{
    public Guid Id { get; init; }

    public Guid HeadOfficeId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    /// <summary>Vergi numarası (DE: Steuernummer / USt-IdNr.) — fatura üstbilgisinde basılır.</summary>
    public string? TaxNumber { get; init; }

    public string DefaultCulture { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public int RoomCount { get; init; }

    public TaxProfileDto TaxProfile { get; init; } = new();
}
