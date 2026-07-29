using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Login kimliği. Kullanıcı bir HeadOffice'e bağlıdır; hangi otelleri görebileceği
/// <see cref="UserHotelAccess"/> ile belirlenir. Roller <see cref="UserRole"/> üzerinden atanır.
/// </summary>
public sealed class User : EntityBase, IAuditableEntity, ISoftDeletable
{
    public Guid HeadOfficeId { get; set; }

    public HeadOffice HeadOffice { get; set; } = null!;

    /// <summary>Sistem genelinde benzersiz; küçük harfe normalize edilerek saklanır.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash. Düz metin parola hiçbir yerde saklanmaz.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Culture { get; set; } = "de";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; } = [];

    public ICollection<UserHotelAccess> HotelAccesses { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
