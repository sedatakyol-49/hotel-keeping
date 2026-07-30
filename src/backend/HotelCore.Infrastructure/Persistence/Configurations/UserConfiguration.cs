using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(8).IsRequired();

        builder.HasOne(x => x.HeadOffice)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.HeadOfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        // E-posta sistem genelinde benzersizdir (login anahtarı). Silinen kullanıcının e-postası
        // tekrar kullanılabilir olmalıdır (personel geri döndüğünde yeni hesap açılabilsin).
        builder.HasIndex(x => x.Email).IsUniqueAmongLiveRows();
        builder.HasIndex(x => x.HeadOfficeId);
    }
}
