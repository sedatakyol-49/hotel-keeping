using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Nationality).HasConversion<string>().HasMaxLength(2);
        builder.Property(x => x.AddressLine).HasMaxLength(256);
        builder.Property(x => x.PostalCode).HasMaxLength(16);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Culture).HasMaxLength(8);
        builder.Property(x => x.Note).HasMaxLength(1000);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.HotelId);
        builder.HasIndex(x => new { x.HotelId, x.LastName, x.FirstName });
        builder.HasIndex(x => new { x.HotelId, x.Email });
    }
}
