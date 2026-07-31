using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class PublicBookingConfiguration : IEntityTypeConfiguration<PublicBooking>
{
    public void Configure(EntityTypeBuilder<PublicBooking> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);

        // Crockford Base32, 12 karakter, tiresiz normalize saklanır.
        builder.Property(x => x.BookingReference).HasMaxLength(16).IsRequired();
        builder.Property(x => x.AccessTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(8).IsRequired();

        builder.Property(x => x.CountryOfResidence).HasConversion<string>().HasMaxLength(2);

        // Sözleşme §6.2: 1–120 karakter, içeriği DOĞRULANMAZ, kaydedilir.
        builder.Property(x => x.OrderButtonLabel).HasMaxLength(120);
        builder.Property(x => x.SummaryHash).HasMaxLength(80).IsRequired();

        builder.Property(x => x.TermsVersion).HasMaxLength(32);
        builder.Property(x => x.PrivacyNoticeVersion).HasMaxLength(32);
        builder.Property(x => x.WithdrawalNoticeVersion).HasMaxLength(32);

        builder.Property(x => x.ConfirmationDocumentHash).HasMaxLength(128);
        builder.Property(x => x.ConfirmationDocumentVersion).HasMaxLength(32);
        builder.Property(x => x.ConfirmationCulture).HasMaxLength(8);

        builder.Property(x => x.CancellationFeeAmount).HasPrecision(18, 2);

        builder.Property(x => x.ConfirmationMode).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Anlık görüntüler sınırsız (text) — gerekçe BookingHoldConfiguration ile aynıdır.
        builder.Property(x => x.OrderSummaryJson).IsRequired();
        builder.Property(x => x.PriceSnapshotJson).IsRequired();
        builder.Property(x => x.CancellationPolicySnapshotJson).IsRequired();
        builder.Property(x => x.LegalSnapshotJson).IsRequired();

        builder.OwnsOne(x => x.InvoiceAddress, address =>
        {
            address.Property(p => p.Company).HasMaxLength(200);
            address.Property(p => p.AddressLine).HasMaxLength(256);
            address.Property(p => p.PostalCode).HasMaxLength(16);
            address.Property(p => p.City).HasMaxLength(100);
            address.Property(p => p.Country).HasConversion<string>().HasMaxLength(2);
            address.Property(p => p.VatId).HasMaxLength(32);

            // Hesaplanan özellik kolon olarak tutulmaz (RefreshToken.IsActive deseni).
            address.Ignore(p => p.HasValue);
        });
        builder.Navigation(x => x.InvoiceAddress).IsRequired();

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Rezervasyon ile bire-bir; FK üzerindeki unique index EF tarafından üretilir.
        builder.HasOne(x => x.Reservation)
            .WithOne(x => x.PublicBooking)
            .HasForeignKey<PublicBooking>(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:1 ilişkinin unique index'i BİLİNÇLİ olarak soft-delete filtresi ALMAZ. Filtre
        // eklenseydi, soft-delete edilmiş bir public booking'in rezervasyonuna İKİNCİ bir
        // public booking yazılabilirdi; o rezervasyon için "hangi rızalar alındı, hangi metin
        // gösterildi" sorusunun iki farklı cevabı olurdu. Kanıt kaydı çoğaltılamaz olmalıdır.
        // Pratikte tetiklenmesi de mümkün değildir: kayıt rezervasyonla aynı transaction'da bir
        // kez yazılır, kullanıcıdan gelen tekrar denemesi yoktur.
        builder.HasIndex(x => x.ReservationId)
            .IsUnique()
            .ExemptFromSoftDeleteFilter(
                "Rezervasyon basina en fazla BIR public booking kanit kaydi olabilir; " +
                "soft-delete edilmis kayit ikinci bir kaydin yazilmasina izin vermemelidir.");

        // Erişim token'ının tekil arama yolu (GET/POST .../bookings/{accessToken}).
        builder.HasIndex(x => x.AccessTokenHash).IsUniqueAmongLiveRows();

        // Referans GLOBAL benzersizdir, otel bazında değil. Gerekçe: bir referans hiçbir zaman
        // iki farklı rezervasyon anlamına gelmemelidir — misafir telefonda referansı söylerken
        // hangi otelde olduğunu bilmeyebilir ve destek personeli yanlış kaydı açmamalıdır.
        // 60 bit entropi ile çakışma pratikte imkânsızdır; kısıt onu KANITLAR.
        builder.HasIndex(x => x.BookingReference).IsUniqueAmongLiveRows();

        builder.HasIndex(x => x.HotelId);
        // Süresi dolan erişimlerin taranması (self-servis erişimin kapanması).
        builder.HasIndex(x => x.AccessTokenExpiresAt);
    }
}
