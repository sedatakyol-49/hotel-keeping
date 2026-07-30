using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.GetById;

/// <summary><c>GET /api/v1/rate-plans/{id}</c> — başka otelin planı 404 döner.</summary>
public sealed record GetRatePlanByIdRequest(Guid Id) : IRequest<RatePlanResponse>;
