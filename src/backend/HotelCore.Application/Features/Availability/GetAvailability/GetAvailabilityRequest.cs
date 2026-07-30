using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Availability.Common;

namespace HotelCore.Application.Features.Availability.GetAvailability;

/// <summary>
/// <c>GET /api/v1/availability?from=&amp;to=&amp;roomTypeId=</c> — aralık boyunca müsait odalar.
/// <para>
/// <c>from</c> giriş, <c>to</c> çıkış günüdür (yarı açık aralık: <c>to</c> gecesi aranmaz).
/// </para>
/// </summary>
public sealed record GetAvailabilityRequest : IRequest<AvailabilityResponse>
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    /// <summary>Opsiyonel oda tipi filtresi.</summary>
    public Guid? RoomTypeId { get; init; }
}
