using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number).HasMaxLength(16).IsRequired();
        builder.Property(x => x.HousekeepingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Hotel)
            .WithMany(x => x.Rooms)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RoomType)
            .WithMany(x => x.Rooms)
            .HasForeignKey(x => x.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Oda numarası otel içinde benzersizdir; silinen odanın numarası tekrar kullanılabilir
        // (kapatılan bir oda yeniden açılabilir) — bu yüzden index yalnızca canlı satırları kapsar.
        builder.HasIndex(x => new { x.HotelId, x.Number }).IsUniqueAmongLiveRows();
        builder.HasIndex(x => x.RoomTypeId);
        // Kat hizmetleri panosu kat + durum kırılımında sorgulanır.
        builder.HasIndex(x => new { x.HotelId, x.Floor, x.HousekeepingStatus });
    }
}
