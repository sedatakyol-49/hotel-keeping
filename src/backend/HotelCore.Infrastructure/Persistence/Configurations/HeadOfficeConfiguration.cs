using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class HeadOfficeConfiguration : IEntityTypeConfiguration<HeadOffice>
{
    public void Configure(EntityTypeBuilder<HeadOffice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BrandName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DefaultCulture).HasMaxLength(8).IsRequired();
        builder.Property(x => x.PublicSlug).HasMaxLength(60);

        builder.HasIndex(x => x.BrandName).IsUnique();

        // Marka sitesi anahtarı. HeadOffice soft-delete edilemez, bu yüzden kısmi filtre
        // gerekmez; PostgreSQL'de NULL değerler benzersizlik kapsamı dışındadır, yani marka
        // sitesi olmayan organizasyonlar birbirini engellemez.
        // Slug BİÇİM kısıtı (regex) migration'da ham SQL olarak eklenir: PostgreSQL'e özgü "~"
        // operatörü modelde durursa handler testlerinin SQLite şeması kurulamaz.
        builder.HasIndex(x => x.PublicSlug).IsUnique();
    }
}
