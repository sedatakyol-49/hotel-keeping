using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Country).HasConversion<string>().HasMaxLength(2).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AddressLine).HasMaxLength(256);
        builder.Property(x => x.PostalCode).HasMaxLength(16);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.TaxNumber).HasMaxLength(32);
        builder.Property(x => x.DefaultCulture).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        // TaxProfile owned type: ayrı tablo değil, Hotel satırında kolon olarak saklanır.
        builder.OwnsOne(x => x.TaxProfile, tax =>
        {
            tax.Property(p => p.VatRate).HasPrecision(5, 2).IsRequired();
            tax.Property(p => p.ReducedVatRate).HasPrecision(5, 2).IsRequired();
            tax.Property(p => p.CityTaxPerPersonNight).HasPrecision(18, 2).IsRequired();
            tax.Property(p => p.CityTaxEnabled).IsRequired();
        });
        builder.Navigation(x => x.TaxProfile).IsRequired();

        // Otel silinmesi Head Office silinmesine bağlanmaz (Restrict) — organizasyon verisi korunur.
        builder.HasOne(x => x.HeadOffice)
            .WithMany(x => x.Hotels)
            .HasForeignKey(x => x.HeadOfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.HeadOfficeId);
        // Otel adı Head Office içinde benzersiz; kapatılan otelin adı yeniden kullanılabilir.
        builder.HasIndex(x => new { x.HeadOfficeId, x.Name }).IsUniqueAmongLiveRows();
        builder.HasIndex(x => x.IsDeleted);
    }
}
