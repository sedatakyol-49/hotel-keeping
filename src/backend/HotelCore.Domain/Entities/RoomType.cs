using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Oda tipi (Odoo: Room Type). Görünen ad/açıklama çok dillidir — çeviriler
/// <see cref="Translation"/> tablosunda (EntityType="RoomType", Field="Name"/"Description") tutulur;
/// buradaki değerler otelin varsayılan dilindeki fallback metinlerdir.
/// </summary>
public sealed class RoomType : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    /// <summary>Kısa kod (SGL, DBL, SUI) — otel içinde benzersiz.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Liste fiyatı (BAR başlangıcı). Kanal/sezon fiyatı için bkz. <see cref="RatePlan"/>.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Maksimum kişi sayısı.</summary>
    public int Capacity { get; set; }

    public int? SizeSqm { get; set; }

    /// <summary>Donanım listesi — virgülle ayrılmış anahtarlar (wifi,minibar,balcony).</summary>
    public string? Amenities { get; set; }

    public ICollection<Room> Rooms { get; } = [];

    public ICollection<RatePlan> RatePlans { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
