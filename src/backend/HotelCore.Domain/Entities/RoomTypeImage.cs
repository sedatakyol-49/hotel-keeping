using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Oda tipi görseli — katalog kartının ve oda tipi detay sayfasının (SEO'nun asıl hedef sayfası)
/// görsel kaynağı.
/// <para>
/// Saklama kararı, boyut kolonları ve çok dilli alt metin gerekçeleri <see cref="HotelImage"/>
/// ile <b>birebir aynıdır</b>; çeviri anahtarı <c>EntityType = "RoomTypeImage"</c>.
/// </para>
/// <para>
/// <b><see cref="HotelId"/> neden ayrıca taşınıyor</b> (oda tipi zaten bir otele ait olduğu
/// hâlde): tenant global query filter'ı <see cref="ITenantEntity"/> üzerinden çalışır. Kolon
/// olmadan görsel tablosu filtreye giremez ve yalnızca JOIN edildiğinde korunurdu — public
/// tarafta tek bir filtresiz sorgu başka otelin görsellerini sızdırırdı. Mevcut <see cref="Room"/>
/// / <see cref="RatePlan"/> ile aynı desen.
/// </para>
/// </summary>
public sealed class RoomTypeImage : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomTypeId { get; set; }

    public RoomType RoomType { get; set; } = null!;

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }

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
