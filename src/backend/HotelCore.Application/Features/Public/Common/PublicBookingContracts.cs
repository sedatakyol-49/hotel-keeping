namespace HotelCore.Application.Features.Public.Common;

/// <summary>Rezervasyon yanıtındaki otel künyesi — GUID <b>taşımaz</b>.</summary>
public sealed record PublicBookingHotelResponse
{
    public string Slug { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string City { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string TimeZoneId { get; init; } = string.Empty;
}

/// <summary>Konaklama bilgisi — <b>oda numarası ve kat yoktur</b> (yasak alan listesi).</summary>
public sealed record PublicBookingStayResponse
{
    public string RoomTypeCode { get; init; } = string.Empty;

    public string RoomTypeName { get; init; } = string.Empty;

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Nights { get; init; }

    public int Adults { get; init; }

    public int Children { get; init; }

    public TimeOnly CheckInFromLocal { get; init; }

    public TimeOnly CheckOutUntilLocal { get; init; }

    public TimeOnly? EstimatedArrivalLocalTime { get; init; }
}

/// <summary>Rezervasyonu yapan misafir — <b>yalnızca kendi</b> verisi.</summary>
public sealed record PublicBookingGuestResponse
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }
}

/// <summary>İptal durumu — politika + gerçekleşen ücret.</summary>
public sealed record PublicBookingCancellationResponse
{
    public string Type { get; init; } = "Flexible";

    public DateTimeOffset FreeCancellationUntil { get; init; }

    public bool IsFreeCancellationAvailable { get; init; }

    public decimal LateCancellationFeePercent { get; init; }

    public decimal LateCancellationFeeAmount { get; init; }

    public decimal NoShowFeePercent { get; init; }

    public decimal NoShowFeeAmount { get; init; }

    public bool CityTaxRefundedOnCancellation { get; init; } = true;

    public string PolicyTextKey { get; init; } = string.Empty;

    /// <summary>Online iptal hâlâ mümkün mü (<c>InHouse</c>/<c>Completed</c>'de <c>false</c>).</summary>
    public bool CanCancelOnline { get; init; }

    /// <summary>İptal edilmediyse <c>null</c>.</summary>
    public decimal? ChargedFeeAmount { get; init; }
}

/// <summary>Ödeme özeti — bu fazda her zaman "girişte ödeme".</summary>
public sealed record PublicBookingPaymentResponse
{
    public string Method { get; init; } = "PayAtProperty";

    public decimal AmountDueAtProperty { get; init; }

    public decimal PrepaidAmount { get; init; }

    /// <summary>Bu fazda her zaman <c>null</c> (PSP yok).</summary>
    public string? Guarantee { get; init; }
}

/// <summary>§312f BGB — onay belgesinin kaydı.</summary>
public sealed record PublicBookingConfirmationResponse
{
    public string Channel { get; init; } = "Email";

    /// <summary>Maskelenmiş alıcı (<c>j***@e***.de</c>) — tam e-posta yanıtta tekrar edilmez.</summary>
    public string RecipientMasked { get; init; } = string.Empty;

    /// <summary>Outbox gönderdikten sonra dolar.</summary>
    public DateTimeOffset? SentAt { get; init; }

    public string? DocumentVersion { get; init; }

    public string Culture { get; init; } = string.Empty;
}

/// <summary>
/// Rezervasyon yanıtı.
/// <para>
/// <b><see cref="AccessToken"/> yalnızca 201 yanıtında doludur.</b> Sonraki okumalarda
/// (<c>GET .../bookings/{accessToken}</c>) alan <c>null</c>'dır: yanıtın loglanması veya
/// paylaşılması hâlinde taşıyıcı kimlik bilgisi tekrar sızmasın diye.
/// </para>
/// </summary>
public sealed record PublicBookingResponse
{
    /// <summary>Crockford Base32, <c>4-4-4</c> gruplu (<c>K7QM-3XPD-9RTV</c>).</summary>
    public string BookingReference { get; init; } = string.Empty;

    /// <summary>base64url, 27 karakter (160 bit). Yalnızca oluşturma yanıtında.</summary>
    public string? AccessToken { get; init; }

    public DateTimeOffset AccessTokenExpiresAt { get; init; }

    /// <summary><c>Confirmed</c> | <c>InHouse</c> | <c>Completed</c> | <c>Cancelled</c> | <c>NoShow</c>.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public PublicBookingHotelResponse Hotel { get; init; } = new();

    public PublicBookingStayResponse Stay { get; init; } = new();

    public PublicBookingGuestResponse Guest { get; init; } = new();

    /// <summary>Rezervasyon anında <b>dondurulmuş</b> fiyat.</summary>
    public PublicPriceResponse Price { get; init; } = new();

    public PublicBookingCancellationResponse Cancellation { get; init; } = new();

    public PublicBookingPaymentResponse Payment { get; init; } = new();

    /// <summary>Hold yanıtındaki <c>legal</c> nesnesinin dondurulmuş kopyası.</summary>
    public PublicLegalNoticesResponse Legal { get; init; } = new();

    public PublicBookingConfirmationResponse Confirmation { get; init; } = new();
}
