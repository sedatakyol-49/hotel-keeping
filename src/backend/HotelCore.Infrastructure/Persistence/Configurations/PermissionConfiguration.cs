using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(256);

        // İzin anahtarı sistem genelinde benzersizdir.
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => x.Module);
    }
}
