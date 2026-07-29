using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(x => x.Id);

        // SHA-256 özeti (hex 64 / base64 44 karakter); 128 payı gelecekteki algoritma değişimi içindir.
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        // IPv6 metin gösterimi en fazla 45 karakter.
        builder.Property(x => x.CreatedByIp).HasMaxLength(45);
        builder.Property(x => x.RevokedByIp).HasMaxLength(45);

        // Kullanıcı silinince token'ları da silinir — saklanacak bir değeri yoktur.
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Rotation zinciri kendine referans; Cascade döngüsü oluşmaması için Restrict.
        builder.HasOne(x => x.ReplacedByToken)
            .WithMany()
            .HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        // Yenileme isteği token'ı özetiyle arar — benzersiz ve tekil arama anahtarı.
        builder.HasIndex(x => x.TokenHash).IsUnique();
        // Kullanıcının aktif token'ları + süresi geçenlerin temizliği.
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });

        // Hesaplanan özellik kolon olarak tutulmaz.
        builder.Ignore(x => x.IsActive);
    }
}
