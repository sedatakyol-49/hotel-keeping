using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>Departman (Reception, Housekeeping, Kitchen, Management ...).</summary>
public sealed class Department : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<Employee> Employees { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
