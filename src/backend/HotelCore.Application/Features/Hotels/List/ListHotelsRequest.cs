using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;

namespace HotelCore.Application.Features.Hotels.List;

/// <summary>
/// <c>GET /api/v1/hotels</c> — kullanıcının erişebildiği oteller. Sayfalama yoktur:
/// bir Head Office'in otel sayısı doğası gereği küçüktür ve liste otel seçiciyi besler.
/// </summary>
public sealed record ListHotelsRequest : IRequest<IReadOnlyList<HotelListItemResponse>>;
