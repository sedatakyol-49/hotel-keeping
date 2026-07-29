using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Field).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();

        // Aynı alan + dil için tek çeviri satırı.
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.Field, x.Culture }).IsUnique();
        // Bir entity'nin tüm çevirilerini tek sorguda çekmek için.
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
