using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Cancel;

/// <summary>
/// <c>POST /api/v1/vacations/{id}/cancel</c> — gövde opsiyoneldir.
/// <para>
/// Yetki: <c>Vacations.Approve</c> (herkesin talebi) <b>veya</b> <c>Vacations.Request</c>
/// (yalnızca kendi talebi). İki alternatifli olduğu için kontrol handler'da yapılır.
/// </para>
/// </summary>
public sealed record CancelVacationRequest : IRequest<VacationRequestResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>İptal notu (opsiyonel).</summary>
    public string? DecisionNote { get; init; }
}
