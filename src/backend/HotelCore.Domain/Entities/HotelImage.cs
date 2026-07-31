using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Otelin tanıtım görseli (misafir sitesi galerisi ve marka listesindeki kapak görseli).
/// <para>
/// <b>Bu fazda yalnızca URL + metadata saklanır</b> (architecture-public-booking.md §7 madde 10,
/// §12): dosya yükleme, yeniden boyutlandırma, blob deposu ve CDN boru hattı <b>yoktur</b>.
/// Şema o genişlemeye hazırdır — bir depolama katmanı eklendiğinde <see cref="Url"/>'in ürettiği
/// değer değişir, <b>sözleşme ve tablo değişmez</b>.
/// </para>
/// <para>
/// <b><see cref="Width"/>/<see cref="Height"/> neden kolon:</b> misafir sitesinde CLS (layout
/// shift) yalnızca boyutlar <i>işaretlemeye</i> yazılırsa önlenir; sunucu görseli indirmeden
/// boyutu ölçemez, dolayısıyla değer içerik girilirken bir kez saklanmak zorundadır.
/// </para>
/// <para>
/// <b><see cref="AltText"/> çok dillidir:</b> buradaki değer otelin varsayılan dilindeki
/// <i>fallback</i>'tir; çeviriler <see cref="Translation"/> tablosunda
/// (<c>EntityType = "HotelImage"</c>, <c>Field = "AltText"</c>) tutulur — <see cref="RoomType"/>
/// ile birebir aynı desen.
/// </para>
/// </summary>
public sealed class HotelImage : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    /// <summary>Görselin mutlak veya köke göreli URL'i.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Galeri sırası (artan). Marka listesindeki tek "kapak" görseli en küçük sıradır —
    /// ayrı bir <c>IsCover</c> bayrağı yoktur, çünkü iki bayrak (sıra + kapak) tutarsızlaşabilir.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>Erişilebilirlik alt metni (varsayılan dilde) — WCAG 1.1.1 gereği zorunlu içeriktir.</summary>
    public string? AltText { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
