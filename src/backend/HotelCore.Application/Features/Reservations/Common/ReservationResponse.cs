namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyon — api-contracts-reservations.md → "Reservations" ile birebir.
/// <para>
/// Türetilmiş alanlar (<c>nights</c>, <c>depositAmount</c>, <c>guestName</c>) <b>sunucuda</b>
/// hesaplanır ki istemciler arasında farklı tanım oluşmasın.
/// </para>
/// </summary>
public sealed record ReservationResponse
{
    public Guid Id { get; init; }

    /// <summary>Misafire iletilen okunur kod (otel içinde benzersiz), örn. <c>RES-2026-00042</c>.</summary>
    public string ReservationNumber { get; init; } = string.Empty;

    /// <summary>Durum enum <b>adı</b>: <c>Option | Confirmed | CheckedIn | CheckedOut | Cancelled | NoShow</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Kanal enum <b>adı</b>: <c>Direct | Phone | WalkIn | BookingCom | Hrs | Expedia | Corporate</c>.</summary>
    public string Channel { get; init; } = string.Empty;

    public Guid RoomId { get; init; }

    public string RoomNumber { get; init; } = string.Empty;

    public Guid RoomTypeId { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    public Guid GuestId { get; init; }

    public string GuestName { get; init; } = string.Empty;

    public string? GuestEmail { get; init; }

    /// <summary>Giriş günü (dahil).</summary>
    public DateOnly CheckIn { get; init; }

    /// <summary>Çıkış günü (<b>dahil değil</b> — bu gün için ücret alınmaz).</summary>
    public DateOnly CheckOut { get; init; }

    /// <summary>Gece sayısı = <c>checkOut - checkIn</c>.</summary>
    public int Nights { get; init; }

    public int Adults { get; init; }

    public int Children { get; init; }

    /// <summary>Konaklamanın toplam brüt tutarı — <b>her zaman sunucuda hesaplanır</b>.</summary>
    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    /// <summary>Ön ödeme yüzdesi (0–100).</summary>
    public decimal DepositPercent { get; init; }

    /// <summary>Ön ödeme tutarı = <c>totalAmount × depositPercent / 100</c> (2 haneye yuvarlı).</summary>
    public decimal DepositAmount { get; init; }

    /// <summary>Tutarın alındığı fiyat planı (geliş gecesi); plan yoksa <c>null</c> (BasePrice).</summary>
    public Guid? RatePlanId { get; init; }

    public string? RatePlanName { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }

    public DateTimeOffset? CheckedOutAt { get; init; }

    /// <summary>Açık hesap kimliği; folio satırları <c>GET /reservations/{id}/folio</c> ile okunur.</summary>
    public Guid? FolioId { get; init; }
}
