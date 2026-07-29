using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Rol. Yetkilendirme rol adına göre değil, rolün taşıdığı izin anahtarlarına göre yapılır
/// (policy-based, architecture.md §7).
/// </summary>
public sealed class Role : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>True ise bu rolün kullanıcıları tüm otelleri görür (Head Office bypass).</summary>
    public bool IsHeadOfficeLevel { get; set; }

    /// <summary>Sistem rolü — seed ile gelir, silinemez.</summary>
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> RolePermissions { get; } = [];

    public ICollection<UserRole> UserRoles { get; } = [];
}
