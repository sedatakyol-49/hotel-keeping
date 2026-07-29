using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>Fiziksel oda. Oda numarası otel içinde benzersizdir.</summary>
public sealed class Room : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomTypeId { get; set; }

    public RoomType RoomType { get; set; } = null!;

    public string Number { get; set; } = string.Empty;

    public int Floor { get; set; }

    /// <summary>Check-out sonrası otomatik <see cref="HousekeepingStatus.Dirty"/> olur.</summary>
    public HousekeepingStatus HousekeepingStatus { get; set; } = HousekeepingStatus.Clean;

    /// <summary>Arızalı/servis dışı — müsaitlik hesabına dahil edilmez.</summary>
    public bool IsOutOfOrder { get; set; }

    public string? Note { get; set; }

    public ICollection<Reservation> Reservations { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
