using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Availability.Common;

namespace HotelCore.Application.Features.Availability.GetOccupancy;

/// <summary>
/// <c>GET /api/v1/occupancy?from=&amp;to=</c> — oda × gün doluluk matrisi (rezervasyon grid'i).
/// Aralık yarı açıktır <c>[from, to)</c> ve
/// <see cref="AvailabilityLimits.MaxOccupancyRangeDays"/> günü aşamaz (aşarsa 400).
/// </summary>
public sealed record GetOccupancyRequest : IRequest<OccupancyResponse>
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }
}
