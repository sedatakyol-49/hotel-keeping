using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.List;

/// <summary>
/// <c>GET /api/v1/rate-plans?roomTypeId=&amp;date=</c> — düz dizi döner (plan sayısı azdır,
/// sözleşme gereği sayfalama yoktur).
/// </summary>
public sealed record ListRatePlansRequest : IRequest<IReadOnlyList<RatePlanResponse>>
{
    /// <summary>Oda tipi filtresi (opsiyonel).</summary>
    public Guid? RoomTypeId { get; init; }

    /// <summary>O gün geçerli olan planlar (<c>validFrom &lt;= date &lt;= validTo</c>).</summary>
    public DateOnly? Date { get; init; }
}
