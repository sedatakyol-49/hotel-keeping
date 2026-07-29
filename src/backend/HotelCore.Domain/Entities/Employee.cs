using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>Çalışan. Login gerekiyorsa <see cref="UserId"/> ile bir <see cref="User"/> ile eşleşir.</summary>
public sealed class Employee : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid DepartmentId { get; set; }

    public Department Department { get; set; } = null!;

    /// <summary>Opsiyonel login ilişkisi — her çalışanın sisteme girişi olmayabilir.</summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Personel numarası (Personalnummer) — otel içinde benzersiz.</summary>
    public string? StaffNumber { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    /// <summary>Yıllık izin hakkı (gün). Yarım günler için ondalık.</summary>
    public decimal AnnualLeaveDays { get; set; }

    public DateOnly HiredOn { get; set; }

    public DateOnly? TerminatedOn { get; set; }

    public ICollection<VacationRequest> VacationRequests { get; } = [];

    public ICollection<VacationBalance> VacationBalances { get; } = [];

    public ICollection<TimeEntry> TimeEntries { get; } = [];

    public ICollection<Shift> Shifts { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
