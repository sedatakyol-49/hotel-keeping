using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>Yıl bazında izin bakiyesi. (EmployeeId, Year) benzersizdir.</summary>
public sealed class VacationBalance : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public int Year { get; set; }

    /// <summary>Hak edilen gün.</summary>
    public decimal EntitledDays { get; set; }

    /// <summary>Kullanılan gün.</summary>
    public decimal UsedDays { get; set; }

    /// <summary>Önceki yıldan devreden gün.</summary>
    public decimal CarriedOverDays { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
