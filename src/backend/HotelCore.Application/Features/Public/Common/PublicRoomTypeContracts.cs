namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Katalogdaki "ab" fiyatı. <c>basis = "BasePrice"</c>: tarih verilmeden sezon fiyatı
/// gösterilemez; PAngV açısından bu bir <b>"ab" fiyatıdır</b> ("ab 120,00 € pro Nacht") ve
/// toplam fiyat iddiası değildir.
/// </summary>
public sealed record PublicFromPriceResponse
{
    public decimal Amount { get; init; }

    public string Currency { get; init; } = "EUR";

    public string Basis { get; init; } = "BasePrice";
}

/// <summary>Katalog kartı — <b>oda sayısı, doluluk ve oda numarası yoktur</b>.</summary>
public sealed record PublicRoomTypeSummaryResponse
{
    /// <summary>Public anahtar; <c>roomTypeId</c> (GUID) <b>dönmez</b>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary><c>Accept-Language</c>'e göre çözülmüş ad (çeviri yoksa varsayılan dile düşer).</summary>
    public string Name { get; init; } = string.Empty;

    public string? ShortDescription { get; init; }

    public int Capacity { get; init; }

    public int? SizeSqm { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public PublicImageResponse? Image { get; init; }

    public PublicFromPriceResponse FromPrice { get; init; } = new();
}

/// <summary>Oda tipi detayı — SEO'nun asıl hedef sayfası.</summary>
public sealed record PublicRoomTypeDetailResponse
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ShortDescription { get; init; }

    public string? Description { get; init; }

    public int Capacity { get; init; }

    public int? SizeSqm { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public IReadOnlyList<PublicImageResponse> Images { get; init; } = [];

    public PublicFromPriceResponse FromPrice { get; init; } = new();

    public PublicHotelCancellationPolicyResponse CancellationPolicy { get; init; } = new();
}

/// <summary>Bir oda tipinin verilen aralıktaki müsaitliği.</summary>
public sealed record PublicOfferAvailabilityResponse
{
    public bool IsAvailable { get; init; }

    /// <summary>
    /// <b>5'te kırpılmış</b> müsait oda sayısı (doluluk ifşası önlenir). Kırpma doğruluğu bozmaz
    /// (UWG §5): gösterilen sayı gerçek bir <i>alt sınırdır</i>.
    /// </summary>
    public int AvailableUnits { get; init; }

    /// <summary><c>true</c> ise "5+" demektir.</summary>
    public bool AvailableUnitsCapped { get; init; }
}

/// <summary>Arama sonucundaki tek teklif.</summary>
public sealed record PublicOfferResponse
{
    public string RoomTypeCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ShortDescription { get; init; }

    public int Capacity { get; init; }

    public int? SizeSqm { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public PublicImageResponse? Image { get; init; }

    public PublicOfferAvailabilityResponse Availability { get; init; } = new();

    public PublicPriceResponse Price { get; init; } = new();

    public PublicCancellationPolicyResponse CancellationPolicy { get; init; } = new();
}

/// <summary>
/// Müsait olmayan oda tipi. <b>Sayı vermez, yalnızca sebep verir</b>: misafire "başka bir
/// tarih/kişi sayısı deneyin" demenin tek doğru yolu hangi kısıtın engellediğini bilmektir.
/// </summary>
public sealed record PublicUnavailableRoomTypeResponse
{
    public string RoomTypeCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary><c>NoRoomAvailable</c> | <c>CapacityExceeded</c> | <c>MinNightsNotMet</c>.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary><c>GET /public/hotels/{hotelSlug}/availability</c> yanıtı.</summary>
public sealed record PublicAvailabilityResponse
{
    public string HotelSlug { get; init; } = string.Empty;

    public string Currency { get; init; } = "EUR";

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Nights { get; init; }

    public int Adults { get; init; }

    public int Children { get; init; }

    /// <summary>Hiçbir tip müsait değilse boş dizi döner — <b>404 değil</b>, 200.</summary>
    public IReadOnlyList<PublicOfferResponse> Offers { get; init; } = [];

    public IReadOnlyList<PublicUnavailableRoomTypeResponse> UnavailableRoomTypes { get; init; } = [];
}
