using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Rezervasyon. Çakışma kontrolü (CheckIn/CheckOut aralığı, aynı oda) uygulama katmanındaki
/// IAvailabilityService tarafından yapılır; sorgu için (HotelId, RoomId, CheckIn, CheckOut) index'i vardır.
/// </summary>
public sealed class Reservation : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomId { get; set; }

    public Room Room { get; set; } = null!;

    public Guid GuestId { get; set; }

    public Guest Guest { get; set; } = null!;

    /// <summary>Fiyatın alındığı plan (opsiyonel; serbest fiyat da girilebilir).</summary>
    public Guid? RatePlanId { get; set; }

    public RatePlan? RatePlan { get; set; }

    /// <summary>Misafire iletilen okunur rezervasyon kodu (otel içinde benzersiz).</summary>
    public string ReservationNumber { get; set; } = string.Empty;

    /// <summary>Giriş günü (takvim günü — otel saat dilimi).</summary>
    public DateOnly CheckIn { get; set; }

    /// <summary>Çıkış günü (dahil değil).</summary>
    public DateOnly CheckOut { get; set; }

    public int Adults { get; set; } = 1;

    public int Children { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Option;

    public ReservationChannel Channel { get; set; } = ReservationChannel.Direct;

    /// <summary>Konaklamanın toplam brüt tutarı.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Ön ödeme yüzdesi (0-100).</summary>
    public decimal DepositPercent { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }

    public DateTimeOffset? CheckedOutAt { get; set; }

    public Folio? Folio { get; set; }

    public ICollection<Invoice> Invoices { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
