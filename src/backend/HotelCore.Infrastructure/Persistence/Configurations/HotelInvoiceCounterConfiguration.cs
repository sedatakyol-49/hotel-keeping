using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class HotelInvoiceCounterConfiguration : IEntityTypeConfiguration<HotelInvoiceCounter>
{
    public void Configure(EntityTypeBuilder<HotelInvoiceCounter> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Prefix).HasMaxLength(16);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Otel + yıl başına tek sayaç satırı; numara bu satır kilitlenerek artırılır.
        builder.HasIndex(x => new { x.HotelId, x.Year }).IsUnique();

        // Optimistic concurrency: eşzamanlı numara verme yarışını yakalar.
        // Değer AppDbContext.SaveChanges içinde otomatik artırılır.
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
