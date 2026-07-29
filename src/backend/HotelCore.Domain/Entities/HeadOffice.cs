using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>Üst organizasyon (marka sahibi). Hiyerarşi: HeadOffice (1) → Hotel (N).</summary>
public sealed class HeadOffice : EntityBase, IAuditableEntity
{
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Varsayılan arayüz dili (de/en/tr).</summary>
    public string DefaultCulture { get; set; } = "de";

    public ICollection<Hotel> Hotels { get; } = [];

    public ICollection<User> Users { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
