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

            // Kurtaxe çocuk muafiyeti. Varsayılan false: mevcut otellerin hesabı
            // ((yetişkin + çocuk) × gece) değişmez — muafiyet opt-in'dir.
            tax.Property(p => p.CityTaxExemptChildren).IsRequired().HasDefaultValue(false);

            // Yaş sınırı belediyeye göre değişir; hesaba girmez (rezervasyonda doğum tarihi yok),
            // faturada/beyanda muafiyetin dayanağı olarak yazdırılır. Bilinmiyorsa NULL.
            tax.Property(p => p.CityTaxChildAgeLimit);
        });
        builder.Navigation(x => x.TaxProfile).IsRequired();

        // Owned type kolonu Hotels tablosunda yaşadığı için kısıt da bu tabloya yazılır.
        // Anlamsız değerleri (negatif yaş, 100+) veritabanı düzeyinde reddeder; Application
        // katmanı ayrıca doğrulama eklemelidir (kullanıcıya anlamlı mesaj için).
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Hotels_CityTaxChildAgeLimit",
            "\"TaxProfile_CityTaxChildAgeLimit\" IS NULL OR " +
            "(\"TaxProfile_CityTaxChildAgeLimit\" >= 0 AND \"TaxProfile_CityTaxChildAgeLimit\" <= 99)"));

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
