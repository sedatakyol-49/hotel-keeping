namespace HotelCore.Application.Features.Public.Common;

/// <summary>§312j Abs. 2 BGB — özetin "wesentliche Merkmale" bileşeni.</summary>
public sealed record PublicEssentialFeaturesResponse
{
    public string RoomTypeName { get; init; } = string.Empty;

    /// <summary>Bu fazda her zaman 1: bir rezervasyon = bir oda (grup rezervasyonu yok).</summary>
    public int RoomCount { get; init; } = 1;

    public PublicOccupancyResponse Occupancy { get; init; } = new();

    /// <summary>Pansiyon modellenmemiştir; sabit <c>"None"</c>.</summary>
    public string Board { get; init; } = "None";
}

/// <summary>Kişi sayısı.</summary>
public sealed record PublicOccupancyResponse
{
    public int Adults { get; init; }

    public int Children { get; init; }
}

/// <summary>§312j Abs. 2 BGB — "Laufzeit des Vertrags" bileşeni.</summary>
public sealed record PublicDurationResponse
{
    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Nights { get; init; }

    public TimeOnly CheckInFromLocal { get; init; }

    public TimeOnly CheckOutUntilLocal { get; init; }

    public string TimeZoneId { get; init; } = string.Empty;
}

/// <summary>§312j Abs. 2 BGB — "Gesamtpreis" bileşeni.</summary>
public sealed record PublicTotalPriceResponse
{
    public decimal Amount { get; init; }

    public string Currency { get; init; } = "EUR";

    public bool VatIncluded { get; init; } = true;

    public bool IncludesMandatoryCharges { get; init; } = true;
}

/// <summary>Özetin tek kalemi — düz metin değil, <b>alan alan</b> verilir.</summary>
public sealed record PublicOrderSummaryComponentResponse
{
    /// <summary><c>Accommodation</c> | <c>CityTax</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public string LabelKey { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public bool Mandatory { get; init; } = true;
}

/// <summary>
/// §312j Abs. 2 BGB — sipariş düğmesinin <b>hemen üstündeki</b> zorunlu özet.
/// <para>
/// Nesne üç zorunlu bileşeni <b>yapısal olarak</b> taşır; düz metin değildir, böylece frontend
/// bir kalemi "unutamaz". <see cref="Hash"/> özetin makineyle zorlanabilir kısmıdır: istemci onu
/// <c>POST /bookings</c> içinde geri gönderir, uyuşmazsa <c>409 SUMMARY_CHANGED</c>.
/// </para>
/// </summary>
public sealed record PublicOrderSummaryResponse
{
    public PublicEssentialFeaturesResponse EssentialFeatures { get; init; } = new();

    public PublicDurationResponse Duration { get; init; } = new();

    public PublicTotalPriceResponse TotalPrice { get; init; } = new();

    public IReadOnlyList<PublicOrderSummaryComponentResponse> Components { get; init; } = [];

    /// <summary>
    /// <c>sha256:</c> + 64 küçük harf hex. Tanım: <b>bu nesnenin, <c>hash</c> alanı hariç</b>,
    /// anahtarları ordinal sıralı, boşluksuz, <c>InvariantCulture</c> sayı biçimli kanonik
    /// JSON'unun SHA-256'sı.
    /// </summary>
    public string Hash { get; init; } = string.Empty;
}

/// <summary>
/// 15 dakikalık geçici tutmanın yanıtı. <c>GET .../holds/{holdToken}</c> <b>birebir aynı şekli</b>
/// döndürür — yeni bir teklif hesaplanmaz, donmuş teklif okunur.
/// </summary>
public sealed record PublicHoldResponse
{
    /// <summary>128-bit, base64url, 22 karakter. Ham değer <b>yalnızca burada</b> döner.</summary>
    public string HoldToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public int ExpiresInSeconds { get; init; }

    public string HotelSlug { get; init; } = string.Empty;

    public string RoomTypeCode { get; init; } = string.Empty;

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Nights { get; init; }

    public int Adults { get; init; }

    public int Children { get; init; }

    public PublicPriceResponse Price { get; init; } = new();

    public PublicCancellationPolicyResponse CancellationPolicy { get; init; } = new();

    public PublicOrderSummaryResponse OrderSummary { get; init; } = new();

    public PublicLegalNoticesResponse Legal { get; init; } = new();

    public IReadOnlyList<PublicPaymentOptionResponse> PaymentOptions { get; init; } = [];

    /// <summary>Veri minimizasyonu: yalnızca ad, soyad, e-posta zorunludur.</summary>
    public IReadOnlyList<string> RequiredGuestFields { get; init; } = [];

    public IReadOnlyList<string> OptionalGuestFields { get; init; } = [];
}
