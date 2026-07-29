using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReservationNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.DepositPercent).HasPrecision(5, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Room)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Guest)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RatePlan)
            .WithMany()
            .HasForeignKey(x => x.RatePlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.HotelId, x.ReservationNumber }).IsUnique();
        // Müsaitlik/çakışma sorgusunun sıcak yolu: aynı odada tarih aralığı kesişimi.
        builder.HasIndex(x => new { x.HotelId, x.RoomId, x.CheckIn, x.CheckOut });
        // Doluluk grid'i ve günlük operasyon listeleri (arrivals/departures).
        builder.HasIndex(x => new { x.HotelId, x.CheckIn });
        builder.HasIndex(x => new { x.HotelId, x.Status });
        builder.HasIndex(x => x.GuestId);
    }
}
