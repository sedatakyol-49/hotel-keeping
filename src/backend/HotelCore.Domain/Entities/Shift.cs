using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>Vardiya planı (planlanan mesai). Gerçekleşen için bkz. <see cref="TimeEntry"/>.</summary>
public sealed class Shift : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    /// <summary>Vardiya günü (takvim günü).</summary>
    public DateOnly Date { get; set; }

    public ShiftType ShiftType { get; set; } = ShiftType.Morning;

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
