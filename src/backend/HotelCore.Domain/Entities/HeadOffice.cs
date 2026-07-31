using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>Üst organizasyon (marka sahibi). Hiyerarşi: HeadOffice (1) → Hotel (N).</summary>
public sealed class HeadOffice : EntityBase, IAuditableEntity
{
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Varsayılan arayüz dili (de/en/tr).</summary>
    public string DefaultCulture { get; set; } = "de";

    /// <summary>
    /// Marka sitesinin URL anahtarı — <c>GET /api/v1/public/brands/{brandSlug}/hotels</c>.
    /// Global benzersizdir. Head Office soft-delete edilemediği için kısmi filtre gerekmez;
    /// <c>null</c> değerler PostgreSQL'de benzersizlik kapsamı dışındadır, yani marka sitesi
    /// olmayan organizasyonlar birbirini engellemez.
    /// </summary>
    public string? PublicSlug { get; set; }

    public ICollection<Hotel> Hotels { get; } = [];

    public ICollection<User> Users { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
