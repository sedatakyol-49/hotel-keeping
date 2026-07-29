using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>İzin talebi (Urlaubsantrag). Onaylandığında ilgili yılın bakiyesi güncellenir.</summary>
public sealed class VacationRequest : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    /// <summary>İzin başlangıcı (takvim günü — saat dilimi taşımaz).</summary>
    public DateOnly From { get; set; }

    /// <summary>İzin bitişi, dahil.</summary>
    public DateOnly To { get; set; }

    /// <summary>Talep edilen iş günü sayısı (yarım gün için ondalık).</summary>
    public decimal RequestedDays { get; set; }

    public VacationStatus Status { get; set; } = VacationStatus.Pending;

    public string? Reason { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
