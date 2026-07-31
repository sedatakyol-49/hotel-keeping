using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Otelin yayımlanmış hukuki belgesi (AGB, Datenschutzerklärung, ...) — dil ve <b>versiyon</b>
/// bazında.
/// <para>
/// <b>Neden ayrı bir entity (ve neden <see cref="Translation"/> yetmez):</b>
/// <list type="number">
///   <item><description><c>GET /public/hotels/{slug}/legal</c> <c>documents[]</c> dizisini
///   <c>key</c>, <c>title</c>, <c>version</c>, <c>culture</c>, <c>bodyHtml</c> ile döndürür —
///   bunlar bir satırın <b>birlikte</b> taşıması gereken alanlardır.</description></item>
///   <item><description>Rezervasyonda onaylanan versiyon (<c>consents.termsVersion</c>) otelin
///   <b>güncel</b> versiyonuyla karşılaştırılır; uyuşmazsa <c>409 LEGAL_TEXT_CHANGED</c>. Bu
///   karşılaştırmanın sorgulanabilir bir "güncel versiyon" kaydına ihtiyacı vardır.</description></item>
///   <item><description>Onaylanan metnin <b>kendisi</b> uyuşmazlıkta kanıttır (DSGVO Art. 7
///   Abs. 1 hesap verebilirlik): eski versiyon silinmez, <see cref="IsActive"/> ile pasife
///   çekilir.</description></item>
///   <item><description><see cref="Translation"/> tablosu 2000 karakterle sınırlıdır ve
///   versiyon kavramı taşımaz; bir AGB metni bu sınırın çok üstündedir.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Belge notu:</b> architecture-public-booking.md §7'deki 15 kalemlik listede bu entity
/// <b>yoktur</b>, ancak sözleşme dosyası §2.3 ve §6.2 onsuz uygulanamaz. Eksik kalem olarak
/// raporlanmıştır.
/// </para>
/// <para>
/// <b><see cref="BodyHtml"/> sanitizasyonu sunucunun sorumluluğudur</b>
/// (api-contracts-public-booking.md §2.3): istemci içeriği <c>innerHTML</c> ile basar. Sanitize
/// edilmiş hâli saklanır — yazma anında bir kez, okuma anında her istekte değil.
/// </para>
/// </summary>
public sealed class HotelLegalDocument : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    /// <summary>Belge anahtarı — <c>terms</c> | <c>privacy</c> | <c>withdrawal</c> | <c>imprint</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Kültür kodu (de, en, tr).</summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Yayın versiyonu — sözleşmede <c>"2026-07-01"</c> gibi <b>opak bir metindir</b>.
    /// Tarih tipi DEĞİLDİR: aynı gün iki kez yayımlanabilir (<c>2026-07-01b</c>) ve versiyon
    /// yalnızca <b>eşitlik</b> için kullanılır; sıralama/aritmetik anlamı yoktur.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Sanitize edilmiş HTML gövde.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>
    /// Bu belge, kendi (anahtar, dil) çifti için <b>güncel</b> yayın mı. Eski versiyonlar
    /// <c>false</c> ile saklanmaya devam eder — onaylanmış metnin kanıtı kaybolmamalıdır.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
