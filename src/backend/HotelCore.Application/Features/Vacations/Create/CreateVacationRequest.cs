using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Create;

/// <summary>
/// <c>POST /api/v1/vacations</c> gövdesi — talep her zaman <c>Pending</c> olarak açılır
/// (durum istekten okunmaz).
/// </summary>
public sealed record CreateVacationRequest : IRequest<VacationRequestResponse>
{
    /// <summary>Aynı otele ait çalışan olmalıdır; aksi hâlde 404.</summary>
    public Guid EmployeeId { get; init; }

    public DateOnly From { get; init; }

    /// <summary>İzin bitişi (dahil); <c>from</c>'dan küçük olamaz.</summary>
    public DateOnly To { get; init; }

    /// <summary>Talep gerekçesi (opsiyonel).</summary>
    public string? Reason { get; init; }
}
