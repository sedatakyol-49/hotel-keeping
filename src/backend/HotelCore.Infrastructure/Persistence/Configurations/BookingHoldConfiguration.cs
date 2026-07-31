using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="BookingHold"/> eşlemesi.
/// <para>
/// <b>Çakışma kısıtı burada DEĞİL, migration'da ham SQL olarak tanımlıdır</b>
/// (<c>EX_BookingHolds_NoOverlappingActiveHolds</c>): EF Core'un <c>EXCLUDE USING gist</c> için
/// bir API'si yoktur ve kısıt <c>daterange</c> ile aralık kesişimi ifade eder — bunu bir unique
/// index taklit edemez.
/// </para>
/// </summary>
public sealed class BookingHoldConfiguration : IEntityTypeConfiguration<BookingHold>
{
    public void Configure(EntityTypeBuilder<BookingHold> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);

        // RefreshToken.TokenHash ile aynı ölçü ve aynı gerekçe (gelecekteki algoritma payı).
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ClientIpHash).HasMaxLength(128);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(8).IsRequired();

        builder.Property(x => x.AccommodationGross).HasPrecision(18, 2);
        builder.Property(x => x.CityTaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalGross).HasPrecision(18, 2);

        // "sha256:" + 64 hex = 71 karakter; 80 küçük bir pay bırakır.
        builder.Property(x => x.SummaryHash).HasMaxLength(80).IsRequired();

        // Dondurulmuş teklif gövdeleri uzunluk SINIRI OLMADAN (text) saklanır: bunlar sözleşme
        // yanıtının birebir kopyasıdır ve yanıt büyüdükçe (gecelik fiyat dizisi, bileşenler)
        // büyür. Keyfi bir sınır, uzun konaklamalarda hold'u sessizce kırardı.
        builder.Property(x => x.PriceSnapshotJson).IsRequired();
        builder.Property(x => x.CancellationPolicySnapshotJson).IsRequired();
        builder.Property(x => x.OrderSummaryJson).IsRequired();
        builder.Property(x => x.LegalSnapshotJson).IsRequired();

        builder.HasOne(x => x.Hotel)
            .WithMany()
            .HasForeignKey(x => x.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RoomType)
            .WithMany()
            .HasForeignKey(x => x.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict (SetNull DEĞİL): SetNull, "tüketildi ama karşılığı yok" hâli üretir ve
        // CK_BookingHolds_ConsumptionIsComplete kısıtını ihlal ederdi. Üretimde rezervasyonlar
        // zaten soft-delete edilir; fiziksel silme yalnızca test/bakım senaryosudur ve orada
        // hold'un önce silinmesi bilinçli bir karar olmalıdır. Süpürücü tüketilmiş hold'ları
        // 24 saat içinde zaten temizler, yani kısıt pratikte hiçbir şeyi bloke etmez.
        builder.HasOne(x => x.ConsumedByReservation)
            .WithMany()
            .HasForeignKey(x => x.ConsumedByReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Gerekçe ReservationConfiguration.CK_Reservations_ValidStay ile birebir aynıdır: boş
        // daterange, hold'u kendi çakışma kısıtından sessizce düşürürdü.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_BookingHolds_ValidStay",
                "\"CheckIn\" < \"CheckOut\"");

            // Tüketilmiş hold'un hangi rezervasyona dönüştüğü bilinmelidir; aksi hâlde
            // "tüketildi ama karşılığı yok" hâli oluşur ve destek bunu çözemez.
            table.HasCheckConstraint(
                "CK_BookingHolds_ConsumptionIsComplete",
                "(\"ConsumedAt\" IS NULL) = (\"ConsumedByReservationId\" IS NULL)");
        });

        // Token ile tekil arama (GET/DELETE /holds/{holdToken}). BookingHold soft-delete
        // EDİLMEZ, bu yüzden filtresiz unique index doğrudur — kısmi filtre uygulanacak bir
        // "silinmiş satır" kavramı yoktur.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        // Müsaitlik hesabı: "bu oda tipinde, bu aralıkta aktif hold'u olan odalar".
        builder.HasIndex(x => new { x.HotelId, x.RoomTypeId, x.CheckIn, x.CheckOut });

        // Süpürücü servisin iki taraması. Kısmi filtreler index'i KÜÇÜK tutar: aktif hold'lar
        // her zaman az sayıdadır, tüketilmişler ise 24 saat içinde silinir.
        builder.HasIndex(x => x.ExpiresAt).HasFilter("\"ConsumedAt\" IS NULL");
        builder.HasIndex(x => x.ConsumedAt).HasFilter("\"ConsumedAt\" IS NOT NULL");
    }
}
