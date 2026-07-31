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
        // USt-IdNr. en uzun AB biçimi 14 karakterdir; 32 mevcut TaxNumber ile simetri içindir.
        builder.Property(x => x.VatId).HasMaxLength(32);
        builder.Property(x => x.DefaultCulture).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        // IANA kimlikleri (Europe/Berlin, America/Argentina/ComodRivadavia) 64'e sığar.
        builder.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CheckInFromLocal).IsRequired();
        builder.Property(x => x.CheckOutUntilLocal).IsRequired();
        // RoomType.Amenities ile aynı biçim ve aynı uzunluk (virgülle ayrılmış i18n anahtarları).
        builder.Property(x => x.Amenities).HasMaxLength(500);

        // Sözleşmedeki desen: ^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$ -> en fazla 60.
        builder.Property(x => x.PublicSlug).HasMaxLength(60);
        // RFC 1035: bir alan adının azami uzunluğu 253 oktettir.
        builder.Property(x => x.PublicHost).HasMaxLength(253);

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

        // Public kanal ayarları — Hotels tablosunda PublicBookingSettings_* kolonları.
        // Varsayılanlar MIGRATION'da verilir (mevcut satırlar için), modelde DB default
        // TANIMLANMAZ: EF, model-default'lu bir alanı CLR varsayılan değerindeyken INSERT'ten
        // düşürür; o zaman "MinAdvanceHours = 0" gibi MEŞRU bir değer sessizce DB default'una
        // dönüşürdü. Değerlerin sahibi C# property initializer'larıdır.
        builder.OwnsOne(x => x.PublicBookingSettings, settings =>
        {
            settings.Property(p => p.IsEnabled).IsRequired();
            settings.Property(p => p.MinNights).IsRequired();
            settings.Property(p => p.MaxNights).IsRequired();
            settings.Property(p => p.MaxAdvanceDays).IsRequired();
            settings.Property(p => p.MinAdvanceHours).IsRequired();
            settings.Property(p => p.MaxAdults).IsRequired();
            settings.Property(p => p.MaxChildren).IsRequired();
            settings.Property(p => p.ConfirmationMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        });
        builder.Navigation(x => x.PublicBookingSettings).IsRequired();

        builder.OwnsOne(x => x.CancellationPolicy, policy =>
        {
            policy.Property(p => p.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            policy.Property(p => p.FreeCancellationDaysBeforeArrival).IsRequired();
            policy.Property(p => p.CutoffLocalTime).IsRequired();
            policy.Property(p => p.LateCancellationFeePercent).HasPrecision(5, 2).IsRequired();
            policy.Property(p => p.NoShowFeePercent).HasPrecision(5, 2).IsRequired();
        });
        builder.Navigation(x => x.CancellationPolicy).IsRequired();

        // §5 DDG künyesi. Tüm alanlar nullable: kanal kapalıyken künye zorunlu değildir,
        // zorunluluk kanal AÇILIRKEN uygulama katmanında doğrulanır (yanıtta anlamlı mesajla).
        builder.OwnsOne(x => x.LegalProfile, legal =>
        {
            legal.Property(p => p.LegalEntityName).HasMaxLength(200);
            legal.Property(p => p.LegalForm).HasMaxLength(50);
            legal.Property(p => p.RepresentedBy).HasMaxLength(200);
            legal.Property(p => p.AddressLine).HasMaxLength(256);
            legal.Property(p => p.PostalCode).HasMaxLength(16);
            legal.Property(p => p.City).HasMaxLength(100);
            legal.Property(p => p.Country).HasConversion<string>().HasMaxLength(2);
            legal.Property(p => p.Phone).HasMaxLength(32);
            legal.Property(p => p.Email).HasMaxLength(256);
            legal.Property(p => p.RegisterCourt).HasMaxLength(150);
            legal.Property(p => p.RegisterNumber).HasMaxLength(50);
            legal.Property(p => p.SupervisoryAuthority).HasMaxLength(200);
            legal.Property(p => p.ParticipatesInDisputeResolution).IsRequired();
            legal.Property(p => p.OnlineDisputeResolutionUrl).HasMaxLength(256);
            legal.Property(p => p.DisputeResolutionNotice).HasMaxLength(1000);
        });
        builder.Navigation(x => x.LegalProfile).IsRequired();

        // Owned type kolonu Hotels tablosunda yaşadığı için kısıt da bu tabloya yazılır.
        // Anlamsız değerleri (negatif yaş, 100+) veritabanı düzeyinde reddeder; Application
        // katmanı ayrıca doğrulama eklemelidir (kullanıcıya anlamlı mesaj için).
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Hotels_CityTaxChildAgeLimit",
                "\"TaxProfile_CityTaxChildAgeLimit\" IS NULL OR " +
                "(\"TaxProfile_CityTaxChildAgeLimit\" >= 0 AND \"TaxProfile_CityTaxChildAgeLimit\" <= 99)");

            // NOT: slug/host BİÇİM kısıtları (regex) burada DEĞİL, migration'da ham SQL olarak
            // tanımlıdır — PostgreSQL'in "~" operatörü modelde durursa handler testlerinin
            // SQLite şeması kurulamaz (bkz. ApplicationTestHost). Aynı gerekçe EXCLUDE kısıtları
            // için de geçerlidir.
            table.HasCheckConstraint(
                "CK_Hotels_PublicBookingSettings",
                "\"PublicBookingSettings_MinNights\" >= 1 " +
                "AND \"PublicBookingSettings_MaxNights\" >= \"PublicBookingSettings_MinNights\" " +
                "AND \"PublicBookingSettings_MaxAdvanceDays\" >= 1 " +
                "AND \"PublicBookingSettings_MinAdvanceHours\" >= 0 " +
                "AND \"PublicBookingSettings_MaxAdults\" >= 1 " +
                "AND \"PublicBookingSettings_MaxChildren\" >= 0");

            // İptal ücreti yüzdelerinin 0–100 kısıtı da MIGRATION'dadır: EF'in SQLite sağlayıcısı
            // decimal'i TEXT olarak saklar, dolayısıyla "BETWEEN 0 AND 100" karşılaştırması
            // handler testlerinin şemasında metin karşılaştırmasına dönüşür ve HER satırı
            // reddeder. Kısıt PostgreSQL'de anlamlıdır ve orada uygulanır.
        });

        // Otel silinmesi Head Office silinmesine bağlanmaz (Restrict) — organizasyon verisi korunur.
        builder.HasOne(x => x.HeadOffice)
            .WithMany(x => x.Hotels)
            .HasForeignKey(x => x.HeadOfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.HeadOfficeId);
        // Otel adı Head Office içinde benzersiz; kapatılan otelin adı yeniden kullanılabilir.
        builder.HasIndex(x => new { x.HeadOfficeId, x.Name }).IsUniqueAmongLiveRows();
        builder.HasIndex(x => x.IsDeleted);

        // Public slug GLOBAL benzersizdir (Head Office bazında değil): URL uzayı globaldir ve
        // /{lang}/{hotelSlug} yolu markadan bağımsızdır. Kısmi index sayesinde (a) NULL slug'lı
        // oteller birbirini engellemez, (b) silinen otelin slug'ı yeniden kullanılabilir.
        // Bu index public tarafın EN SICAK yoludur: her istek slug ile oteli bulur.
        builder.HasIndex(x => x.PublicSlug).IsUniqueAmongLiveRows();
        // Edge/SSR katmanının host -> slug çevirisi; bir host tek bir oteli gösterebilir.
        builder.HasIndex(x => x.PublicHost).IsUniqueAmongLiveRows();
    }
}
