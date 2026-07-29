using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class VacationBalanceConfiguration : IEntityTypeConfiguration<VacationBalance>
{
    public void Configure(EntityTypeBuilder<VacationBalance> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntitledDays).HasPrecision(5, 2);
        builder.Property(x => x.UsedDays).HasPrecision(5, 2);
        builder.Property(x => x.CarriedOverDays).HasPrecision(5, 2);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.VacationBalances)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Çalışan başına yıl bazında tek bakiye satırı.
        builder.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();
        builder.HasIndex(x => x.HotelId);
    }
}
