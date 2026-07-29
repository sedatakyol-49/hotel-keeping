using System.Diagnostics.CodeAnalysis;

namespace HotelCore.Domain.Entities;

/// <summary>Rol ↔ İzin çoka-çok bağlantı tablosu (composite anahtar, EntityBase türetmez).</summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Entity adı mimari dokümanda (architecture.md §4.5) sözleşme olarak tanımlı; CAS Permission tipiyle ilgisi yok.")]
public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
