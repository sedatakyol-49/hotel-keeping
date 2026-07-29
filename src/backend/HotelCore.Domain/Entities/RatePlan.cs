using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>Fiyat planı (BAR / sezon / kanal bazlı). Bir oda tipine bağlıdır.</summary>
public sealed class RatePlan : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomTypeId { get; set; }

    public RoomType RoomType { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Gecelik fiyat (otelin para biriminde).</summary>
    public decimal Price { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    /// <summary>Null ise tüm kanallar için geçerlidir.</summary>
    public ReservationChannel? Channel { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
