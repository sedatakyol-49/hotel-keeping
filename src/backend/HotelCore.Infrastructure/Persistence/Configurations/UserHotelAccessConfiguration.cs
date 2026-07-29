using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class UserHotelAccessConfiguration : IEntityTypeConfiguration<UserHotelAccess>
{
    public void Configure(EntityTypeBuilder<UserHotelAccess> builder)
    {
        builder.HasKey(x => new { x.UserId, x.HotelId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.HotelAccesses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Otel kaydı korunur; erişim satırı otel silinince otomatik silinmez.
        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.HotelId);
    }
}
