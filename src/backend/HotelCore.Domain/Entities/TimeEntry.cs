using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Zeiterfassung — fiili giriş/çıkış kaydı. Bu fazda kayıt manuel web üzerinden yapılır
/// (<see cref="TimeEntrySource.Manual"/>); tüm zamanlar UTC saklanır.
/// </summary>
public sealed class TimeEntry : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public DateTimeOffset ClockIn { get; set; }

    /// <summary>Açık kayıtlarda null (çalışan hâlâ mesaide).</summary>
    public DateTimeOffset? ClockOut { get; set; }

    public int BreakMinutes { get; set; }

    public TimeEntrySource Source { get; set; } = TimeEntrySource.Manual;

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
