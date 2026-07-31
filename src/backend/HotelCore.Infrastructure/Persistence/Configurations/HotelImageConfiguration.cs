using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class HotelImageConfiguration : IEntityTypeConfiguration<HotelImage>
{
    public void Configure(EntityTypeBuilder<HotelImage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);

        // 1024: imzalı CDN URL'leri (sorgu parametreli) rahatça sığsın diye.
        builder.Property(x => x.Url).HasMaxLength(1024).IsRequired();
        // Alt metin ekran okuyucu için kısa ve betimleyici olmalıdır; 300 fazlasıyla yeterli.
        builder.Property(x => x.AltText).HasMaxLength(300);
        builder.Property(x => x.SortOrder).IsRequired();

        builder.HasOne(x => x.Hotel)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Galeri okuma yolu: otelin görselleri sıraya göre. Tek index hem tenant filtresini
        // hem ORDER BY'ı karşılar.
        builder.HasIndex(x => new { x.HotelId, x.SortOrder });
    }
}
