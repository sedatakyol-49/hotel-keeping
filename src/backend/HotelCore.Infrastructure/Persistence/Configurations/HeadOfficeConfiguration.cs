using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class HeadOfficeConfiguration : IEntityTypeConfiguration<HeadOffice>
{
    public void Configure(EntityTypeBuilder<HeadOffice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BrandName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DefaultCulture).HasMaxLength(8).IsRequired();

        builder.HasIndex(x => x.BrandName).IsUnique();
    }
}
