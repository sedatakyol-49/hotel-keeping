using System.Diagnostics.CodeAnalysis;
using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>Granüler izin. <see cref="Key"/> değerleri <c>Permissions</c> sabitlerinden gelir.</summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Entity adı mimari dokümanda (architecture.md §4.5) sözleşme olarak tanımlı; CAS Permission tipiyle ilgisi yok.")]
public sealed class Permission : EntityBase
{
    /// <summary>Modül.Aksiyon formatında benzersiz anahtar (örn. Invoices.Approve).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Anahtarın modül kısmı — UI gruplaması için.</summary>
    public string Module { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; } = [];
}
