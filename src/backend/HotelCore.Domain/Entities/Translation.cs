using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Dinamik içerik çevirisi (architecture.md §4.6): (EntityType, EntityId, Field, Culture) → Text.
/// Statik UI metinleri burada TUTULMAZ (frontend ngx-translate JSON, backend .resx).
/// Kayıt zaten otele bağlı bir satıra (EntityId) işaret ettiği için ayrıca HotelId taşımaz.
/// </summary>
public sealed class Translation : EntityBase
{
    /// <summary>Çevrilen entity tipinin adı (örn. RoomType).</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Çevrilen alan adı (örn. Name, Description).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Kültür kodu (de, en, tr).</summary>
    public string Culture { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
