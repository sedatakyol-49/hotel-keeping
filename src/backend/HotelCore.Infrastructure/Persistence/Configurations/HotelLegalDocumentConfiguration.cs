using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class HotelLegalDocumentConfiguration : IEntityTypeConfiguration<HotelLegalDocument>
{
    public void Configure(EntityTypeBuilder<HotelLegalDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        // Uzunluk SINIRLANMAZ (text): bir AGB veya Datenschutzerklärung metni onlarca bin
        // karakter olabilir; keyfi bir üst sınır belgeyi ortasından keserdi.
        builder.Property(x => x.BodyHtml).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasOne(x => x.Hotel)
            .WithMany(x => x.LegalDocuments)
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Aynı (otel, belge, dil) için bir versiyon yalnızca bir kez yayımlanır.
        builder.HasIndex(x => new { x.HotelId, x.Key, x.Culture, x.Version }).IsUniqueAmongLiveRows();

        // GET /legal'in okuma yolu: otelin GÜNCEL belgeleri. Kısmi index yalnızca aktif ve
        // silinmemiş satırları taşır — tablo versiyon geçmişiyle büyüse de index büyümez.
        builder.HasIndex(x => new { x.HotelId, x.Key, x.Culture })
            .HasFilter("\"IsActive\" AND NOT \"IsDeleted\"");
    }
}
