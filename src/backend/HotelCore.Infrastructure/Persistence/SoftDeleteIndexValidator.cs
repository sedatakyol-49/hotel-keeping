using HotelCore.Domain.Common;
using HotelCore.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HotelCore.Infrastructure.Persistence;

/// <summary>
/// Model kurulumunda çalışan regresyon guard'ı: <see cref="ISoftDeletable"/> uygulayan bir
/// entity'de <c>IsDeleted</c> filtresi olmayan unique index bırakılırsa uygulama <b>açılışta</b>
/// hata verir.
/// <para>
/// <b>Neden burada:</b> bu hata çalışma zamanında ancak "silinmiş kaydın doğal anahtarı tekrar
/// kullanıldığında" ortaya çıkar (HTTP 500 + kullanılamaz hâle gelen oda numarası). Test kapsamı
/// dışında kalması kolay olduğu için kural modelin kendisinde zorunlu tutulur: yeni bir unique
/// index eklerken filtreyi unutan geliştirici ilk <c>OnModelCreating</c>'te (dolayısıyla ilk
/// istek, ilk migration ve tüm testlerde) net bir mesajla durdurulur.
/// </para>
/// </summary>
internal static class SoftDeleteIndexValidator
{
    /// <summary>
    /// Tüm soft-delete edilebilir entity'lerin unique index'lerini denetler.
    /// Muaf tutulanlar <see cref="SoftDeleteIndexExtensions.ExemptionAnnotation"/> ile işaretlidir.
    /// </summary>
    /// <exception cref="InvalidOperationException">Filtresiz ve muaf olmayan unique index varsa.</exception>
    public static void Validate(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        List<string>? violations = null;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            foreach (var index in entityType.GetIndexes())
            {
                if (!index.IsUnique || IsExempt(index) || HasSoftDeleteFilter(index))
                {
                    continue;
                }

                var columns = string.Join(", ", index.Properties.Select(property => property.Name));
                violations ??= [];
                violations.Add($"{entityType.ClrType.Name}({columns})");
            }
        }

        if (violations is null)
        {
            return;
        }

        throw new InvalidOperationException(
            "Soft-delete edilebilir entity'lerde filtresiz unique index bulundu: " +
            string.Join("; ", violations) +
            $". Bu index'ler silinmis satirlari da kapsar; ön kontrol soft-deleted satiri gormedigi " +
            $"icin kullaniciya 409 yerine 500 doner ve silinen kaydin dogal anahtari tekrar " +
            $"kullanilamaz. Cozum: HasIndex(...).{nameof(SoftDeleteIndexExtensions.IsUniqueAmongLiveRows)}() " +
            $"kullanin; benzersizligin silinen satirlari da kapsamasi gerekiyorsa " +
            $"{nameof(SoftDeleteIndexExtensions.ExemptFromSoftDeleteFilter)}(gerekce) ile muafiyeti belgeleyin.");
    }

    private static bool IsExempt(IReadOnlyIndex index) =>
        index.FindAnnotation(SoftDeleteIndexExtensions.ExemptionAnnotation) is not null;

    /// <summary>
    /// Filtrenin <c>IsDeleted</c> kolonuna değindiğini doğrular. Metin araması bilinçlidir:
    /// filtre ham SQL olduğu için (birleşik koşullar dâhil) tek güvenilir kontrol budur.
    /// </summary>
    private static bool HasSoftDeleteFilter(IReadOnlyIndex index) =>
        index.GetFilter()?.Contains(SoftDeleteIndexExtensions.SoftDeleteColumnName, StringComparison.Ordinal) == true;
}
