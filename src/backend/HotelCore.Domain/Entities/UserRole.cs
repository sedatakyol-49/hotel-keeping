namespace HotelCore.Domain.Entities;

/// <summary>Kullanıcı ↔ Rol çoka-çok bağlantı tablosu (composite anahtar).</summary>
public sealed class UserRole
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;
}
