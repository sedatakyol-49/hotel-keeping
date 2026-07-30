using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Reject;

/// <summary>
/// <c>POST /api/v1/vacations/{id}/reject</c> — gövde opsiyoneldir; <c>decisionNote</c> ret
/// gerekçesini taşır. Ret bakiyeyi <b>etkilemez</b> (gün hiç düşülmemiştir).
/// </summary>
public sealed record RejectVacationRequest : IRequest<VacationRequestResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>Ret gerekçesi (opsiyonel).</summary>
    public string? DecisionNote { get; init; }
}
