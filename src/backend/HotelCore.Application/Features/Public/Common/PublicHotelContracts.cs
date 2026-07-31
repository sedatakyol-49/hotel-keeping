namespace HotelCore.Application.Features.Public.Common;

/// <summary>Marka sitesinin otel listesi satırı (<c>GET /public/brands/{brandSlug}/hotels</c>).</summary>
public sealed record PublicHotelListItemResponse
{
    public string Slug { get; init; } = string.Empty;

    /// <summary>Otel adı — <b>hardcode değil</b>, <c>Hotel.Name</c>'den gelir.</summary>
    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    /// <summary>Ülke enum <b>adı</b> (<c>DE</c> | <c>AT</c> | …), sayı değil.</summary>
    public string Country { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public string DefaultCulture { get; init; } = string.Empty;

    public string? ShortDescription { get; init; }

    /// <summary>Kapak görseli = en küçük <c>sortOrder</c>; ayrı bir "isCover" bayrağı yoktur.</summary>
    public PublicImageResponse? Image { get; init; }
}

/// <summary>Public kanal ayarları — formun sınırlarını istemciye bildirir.</summary>
public sealed record PublicBookingSettingsResponse
{
    public int MinNights { get; init; }

    public int MaxNights { get; init; }

    public int MaxAdvanceDays { get; init; }

    /// <summary><c>0</c> = aynı gün rezervasyon serbest.</summary>
    public int MinAdvanceHours { get; init; }

    public int MaxAdults { get; init; }

    public int MaxChildren { get; init; }

    /// <summary><c>Instant</c> | <c>OnHotelAcceptance</c>.</summary>
    public string ConfirmationMode { get; init; } = "Instant";
}

/// <summary>Otelin Kurtaxe künyesi — <b>koda gömülü değil</b>, <c>Hotel.TaxProfile</c>'dan.</summary>
public sealed record PublicCityTaxInfoResponse
{
    public bool Applies { get; init; }

    public decimal PerPersonNight { get; init; }

    public string Currency { get; init; } = "EUR";

    public bool ChildrenExempt { get; init; }

    /// <summary>Bilgilendirmedir; hesaba girmez.</summary>
    public int? ChildAgeLimit { get; init; }

    public bool ChargedOnlyIfStayTakesPlace { get; init; } = true;
}

/// <summary>Otel künyesi — <c>GET /public/hotels/{hotelSlug}</c>.</summary>
public sealed record PublicHotelResponse
{
    public string Slug { get; init; } = string.Empty;

    /// <summary><c>HeadOffice.BrandName</c> — marka adı hardcode edilmez.</summary>
    public string BrandName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string City { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string Currency { get; init; } = string.Empty;

    /// <summary>IANA saat dilimi — misafirin yerel saatleri buna göre yorumlanır.</summary>
    public string TimeZoneId { get; init; } = string.Empty;

    public string DefaultCulture { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedCultures { get; init; } = [];

    public TimeOnly CheckInFromLocal { get; init; }

    public TimeOnly CheckOutUntilLocal { get; init; }

    public IReadOnlyList<PublicImageResponse> Images { get; init; } = [];

    /// <summary>i18n anahtarları (<c>wifi</c>, <c>parking</c> …) — serbest metin değildir.</summary>
    public IReadOnlyList<string> Amenities { get; init; } = [];

    public PublicBookingSettingsResponse Booking { get; init; } = new();

    public PublicCityTaxInfoResponse CityTax { get; init; } = new();

    public PublicHotelCancellationPolicyResponse CancellationPolicy { get; init; } = new();

    public IReadOnlyList<PublicPaymentOptionResponse> PaymentOptions { get; init; } = [];
}

/// <summary>
/// Otel künyesindeki <b>tarihsiz</b> iptal politikası (mutlak son tarih ancak konaklama tarihi
/// bilinince hesaplanabilir — bkz. <see cref="PublicCancellationPolicyResponse"/>).
/// </summary>
public sealed record PublicHotelCancellationPolicyResponse
{
    public string Type { get; init; } = "Flexible";

    public int FreeCancellationDaysBeforeArrival { get; init; }

    public TimeOnly CutoffLocalTime { get; init; }

    public decimal LateCancellationFeePercent { get; init; }

    public decimal NoShowFeePercent { get; init; }

    /// <summary>Kurtaxe ceza matrahına <b>girmez</b> — bu bir ayar değil, bir değişmezdir.</summary>
    public bool AppliesToAccommodationOnly { get; init; } = true;
}

/// <summary>§5 DDG Impressum künyesi — <b>tamamı veritabanından</b>, hardcode yok.</summary>
public sealed record PublicImprintResponse
{
    public string? LegalEntityName { get; init; }

    public string? LegalForm { get; init; }

    public string? RepresentedBy { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public string? Country { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? RegisterCourt { get; init; }

    public string? RegisterNumber { get; init; }

    /// <summary>USt-IdNr. — <c>Hotel.VatId</c>; Steuernummer'dan <b>ayrı</b> alandır.</summary>
    public string? VatId { get; init; }

    public string? SupervisoryAuthority { get; init; }

    public PublicDisputeResolutionResponse DisputeResolution { get; init; } = new();
}

/// <summary>ODR / VSBG bildirimi — katılmayan işletme de bildirmek <b>zorundadır</b> (§36 VSBG).</summary>
public sealed record PublicDisputeResolutionResponse
{
    public bool ParticipatesInAdr { get; init; }

    public string NoticeKey { get; init; } = "legal.adr.notParticipating";

    public string? Notice { get; init; }

    public string? OdrPlatformUrl { get; init; }
}

/// <summary>Yayımlanmış hukuki belge (AGB, Datenschutzerklärung, Widerrufshinweis).</summary>
public sealed record PublicLegalDocumentResponse
{
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>Opak metin (<c>"2026-07-01"</c>); rızada <b>aynen</b> kullanılır.</summary>
    public string Version { get; init; } = string.Empty;

    public string Culture { get; init; } = string.Empty;

    /// <summary><b>Sunucuda sanitize edilmiş</b> HTML — istemci <c>innerHTML</c> ile basar.</summary>
    public string BodyHtml { get; init; } = string.Empty;
}

/// <summary><c>GET /public/hotels/{hotelSlug}/legal</c> yanıtı.</summary>
public sealed record PublicLegalResponse
{
    public PublicImprintResponse Imprint { get; init; } = new();

    public IReadOnlyList<PublicLegalDocumentResponse> Documents { get; init; } = [];

    /// <summary>Cayma hakkı bildirimi (hakkın <b>bulunmadığı</b> bilgisi + versiyon).</summary>
    public PublicWithdrawalRightResponse WithdrawalRight { get; init; } = new();
}
