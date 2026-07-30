using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Approve;

/// <summary>
/// <c>POST /api/v1/vacations/{id}/approve</c> — gövde opsiyoneldir (yalnızca not).
/// </summary>
public sealed record ApproveVacationRequest : IRequest<VacationRequestResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>Onay notu (opsiyonel).</summary>
    public string? DecisionNote { get; init; }
}
