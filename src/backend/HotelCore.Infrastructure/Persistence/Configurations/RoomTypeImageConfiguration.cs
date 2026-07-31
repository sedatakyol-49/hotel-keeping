using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class RoomTypeImageConfiguration : IEntityTypeConfiguration<RoomTypeImage>
{
    public void Configure(EntityTypeBuilder<RoomTypeImage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.AltText).HasMaxLength(300);
        builder.Property(x => x.SortOrder).IsRequired();

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RoomType)
            .WithMany(x => x.Images)
            .HasForeignKey(x => x.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Katalog ve detay sayfasının okuma yolu: oda tipinin görselleri sıraya göre.
        builder.HasIndex(x => new { x.RoomTypeId, x.SortOrder });
        // Tenant filtresi HotelId üzerinden çalışır; otel bazlı toplu okuma için ayrı index.
        builder.HasIndex(x => x.HotelId);
    }
}
