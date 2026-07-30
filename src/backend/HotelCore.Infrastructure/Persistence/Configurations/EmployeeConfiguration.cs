using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.StaffNumber).HasMaxLength(32);
        builder.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.AnnualLeaveDays).HasPrecision(5, 2);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Department)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Login ilişkisi opsiyoneldir; kullanıcı silinse bile personel kaydı kalır.
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.HotelId);
        builder.HasIndex(x => new { x.HotelId, x.DepartmentId });
        builder.HasIndex(x => new { x.HotelId, x.LastName });
        // Personel numarası otel içinde benzersiz; işten çıkan personelin kaydı silindiğinde
        // numara yeniden verilebilir (numara blokları tükenmesin).
        builder.HasIndex(x => new { x.HotelId, x.StaffNumber }).IsUniqueAmongLiveRows();
        builder.HasIndex(x => x.UserId);
    }
}
