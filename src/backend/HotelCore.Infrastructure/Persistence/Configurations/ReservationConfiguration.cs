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

        // En az bir gece. Bu kısıt yalnızca "day-use yok" kuralını tekrar etmez; ÇAKIŞMA
        // KISITININ ÖN ŞARTIDIR: CheckIn = CheckOut olduğunda daterange BOŞ aralık üretir, boş
        // aralık hiçbir şeyle çakışmaz ve o satır EX_Reservations_NoOverlappingStays kapsamından
        // sessizce düşerdi. CheckIn > CheckOut ise daterange doğrudan hata fırlatır (22000) ve
        // kullanıcı anlamsız bir veritabanı hatası görürdü.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Reservations_ValidStay",
            "\"CheckIn\" < \"CheckOut\""));

        // Rezervasyon numarası otel içinde benzersiz. Rezervasyon GoBD belgesi DEĞİLDİR (fatura
        // öyledir), bu yüzden silinmiş kayıtlar benzersizlik kapsamı dışında bırakılır; iptal
        // edilen rezervasyon silinmez, Status = Cancelled ile durur ve numarasını korur.
        builder.HasIndex(x => new { x.HotelId, x.ReservationNumber }).IsUniqueAmongLiveRows();
        // Müsaitlik/çakışma sorgusunun sıcak yolu: aynı odada tarih aralığı kesişimi.
        builder.HasIndex(x => new { x.HotelId, x.RoomId, x.CheckIn, x.CheckOut });
        // Doluluk grid'i ve günlük operasyon listeleri (arrivals/departures).
        builder.HasIndex(x => new { x.HotelId, x.CheckIn });
        builder.HasIndex(x => new { x.HotelId, x.Status });
        builder.HasIndex(x => x.GuestId);
    }
}
