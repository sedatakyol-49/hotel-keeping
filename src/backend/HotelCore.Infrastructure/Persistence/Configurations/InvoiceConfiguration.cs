using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.NetAmount).HasPrecision(18, 2);
        builder.Property(x => x.VatAmount).HasPrecision(18, 2);
        builder.Property(x => x.CityTaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Reservation)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Guest)
            .WithMany()
            .HasForeignKey(x => x.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Storno zinciri: orijinal fatura -> kendisini iptal eden fatura (kendine referans).
        builder.HasOne(x => x.CancelledByInvoice)
            .WithMany()
            .HasForeignKey(x => x.CancelledByInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // GoBD: fatura numarası otel bazında benzersiz ve boşluksuzdur.
        // Taslak faturalar numarasızdır; PostgreSQL'de birden çok boş string olamayacağı için
        // unique index yalnızca numarası atanmış (finalize edilmiş) satırlara uygulanır.
        //
        // Bu index KASITLI olarak soft-delete filtresi ALMAZ (diğer tüm ISoftDeletable unique
        // index'lerinin aksine): fatura numarası, kayıt IsDeleted işaretlenmiş olsa bile 10 yıllık
        // saklama süresi boyunca tek ve tekrarsız kalmalıdır (GoBD "Einmaligkeit der
        // Belegnummer"). Filtre eklenirse silinmiş görünen bir faturanın numarası yeniden
        // verilebilir hâle gelir ve aynı numaraya sahip iki belge denetim izini bozar. Zaten
        // AppDbContext.EnforceInvoiceImmutability fatura silmeyi tamamen reddettiği için burada
        // "silinen numaranın yeniden kullanılabilmesi" gibi bir işletme ihtiyacı da yoktur.
        builder.HasIndex(x => new { x.HotelId, x.InvoiceNumber })
            .IsUnique()
            .HasFilter("\"InvoiceNumber\" <> ''")
            .ExemptFromSoftDeleteFilter(
                "GoBD: fatura numarasi silinmis satirlar dahil tum saklama suresi boyunca benzersiz kalmalidir.");

        builder.HasIndex(x => new { x.HotelId, x.Status });
        builder.HasIndex(x => new { x.HotelId, x.IssuedAt });
        builder.HasIndex(x => x.GuestId);
        builder.HasIndex(x => x.ReservationId);
    }
}
