using HotelCore.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

/// <summary>
/// Soft-delete edilebilen (<see cref="ISoftDeletable"/>) entity'lerin benzersizlik kısıtları için
/// <b>tek doğruluk kaynağı</b>. Filtre ifadesi burada üretilir; konfigürasyonlara elle string
/// kopyalanmaz.
/// <para>
/// <b>Neden gerekli:</b> global query filter <c>!IsDeleted</c> koşulunu her sorguya eklediği için
/// handler'ların çakışma ön kontrolü soft-deleted satırı görmez. Unique index filtresiz olursa
/// ön kontrol geçer, veritabanı 23505 ile patlar ve kullanıcı 409 yerine 500 alır. Ayrıca silinen
/// bir kaydın doğal anahtarı (örn. oda numarası) bir daha asla kullanılamaz — otel işletmesinde
/// kapatılan bir oda tekrar açılabilir olmalıdır.
/// </para>
/// <para>
/// Kural <see cref="SoftDeleteIndexValidator"/> tarafından model kurulumunda zorunlu tutulur;
/// meşru istisnalar <see cref="ExemptFromSoftDeleteFilter{TEntity}"/> ile gerekçesiyle işaretlenir.
/// </para>
/// </summary>
internal static class SoftDeleteIndexExtensions
{
    /// <summary>
    /// Kısmi (partial) index koşulu. Yalnızca canlı satırlar index'e girer; PostgreSQL'de
    /// <c>CREATE UNIQUE INDEX ... WHERE NOT "IsDeleted"</c> olarak üretilir.
    /// </summary>
    public const string NotDeletedPredicate = "NOT \"IsDeleted\"";

    /// <summary>
    /// Doğrulayıcının filtre ararken eşleştirdiği kolon adı. Hem bu sınıfın ürettiği ifadede hem de
    /// elle yazılmış birleşik filtrelerde bu adın geçmesi beklenir.
    /// </summary>
    public const string SoftDeleteColumnName = nameof(ISoftDeletable.IsDeleted);

    /// <summary>
    /// Filtresiz unique index'e bilinçli olarak izin verildiğini belirten annotation anahtarı.
    /// Değeri gerekçe metnidir (denetimde okunabilir olması için).
    /// </summary>
    public const string ExemptionAnnotation = "HotelCore:SoftDeleteUniqueIndexExemption";

    /// <summary>
    /// Index'i <b>yalnızca silinmemiş satırlar arasında</b> benzersiz yapar.
    /// </summary>
    /// <param name="additionalPredicate">
    /// Varsa ek SQL koşulu (örn. <c>"InvoiceNumber" &lt;&gt; ''</c>); <c>NOT "IsDeleted"</c> ile
    /// AND'lenir.
    /// </param>
    public static IndexBuilder<TEntity> IsUniqueAmongLiveRows<TEntity>(
        this IndexBuilder<TEntity> builder,
        string? additionalPredicate = null)
        where TEntity : class, ISoftDeletable
    {
        ArgumentNullException.ThrowIfNull(builder);

        var filter = string.IsNullOrWhiteSpace(additionalPredicate)
            ? NotDeletedPredicate
            : $"({additionalPredicate}) AND {NotDeletedPredicate}";

        return builder.IsUnique().HasFilter(filter);
    }

    /// <summary>
    /// Soft-delete filtresi <b>bilinçli olarak</b> eklenmeyen unique index'i işaretler; aksi hâlde
    /// <see cref="SoftDeleteIndexValidator"/> model kurulumunda hata fırlatır. Gerekçe zorunludur.
    /// </summary>
    public static IndexBuilder<TEntity> ExemptFromSoftDeleteFilter<TEntity>(
        this IndexBuilder<TEntity> builder,
        string reason)
        where TEntity : class, ISoftDeletable
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return builder.HasAnnotation(ExemptionAnnotation, reason);
    }
}
