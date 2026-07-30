using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class RatePlanConfiguration : IEntityTypeConfiguration<RatePlan>
{
    public void Configure(EntityTypeBuilder<RatePlan> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RoomType)
            .WithMany(x => x.RatePlans)
            .HasForeignKey(x => x.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Geçerlilik aralığı kapalıdır [ValidFrom, ValidTo] ve ters aralık anlamsızdır.
        // Bu kısıt aynı zamanda çakışma kısıtının ÖN KOŞULUdur: PostgreSQL'de
        // daterange(ValidFrom, ValidTo, '[]') alt sınır > üst sınır olduğunda hata fırlatır,
        // yani kısıt olmadan bozuk bir satır anlaşılmaz bir 500 üretirdi.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_RatePlans_ValidRange",
            "\"ValidFrom\" <= \"ValidTo\""));

        builder.HasIndex(x => x.HotelId);
        // Tarih aralığı + kanal eşleşmesi fiyat arama sorgusunun sıcak yoludur.
        builder.HasIndex(x => new { x.RoomTypeId, x.ValidFrom, x.ValidTo });

        // ÇAKIŞMA KISITI: aynı (RoomTypeId, Channel) için tarih aralığı çakışan iki AKTİF plan
        // olamaz. Bu kısıt EF ile ifade edilemez (PostgreSQL "EXCLUDE USING gist" + daterange
        // gerektirir) ve migration içinde HAM SQL olarak eklenmiştir:
        // Persistence/Migrations/*_CloseDomainGapsForInvoicingAndRates.cs.
        // İhlali SQLSTATE 23P01 üretir; AppDbContext bunu 409 Conflict'e çevirir.
    }
}
