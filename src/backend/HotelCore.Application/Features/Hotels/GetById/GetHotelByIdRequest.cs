using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;

namespace HotelCore.Application.Features.Hotels.GetById;

/// <summary><c>GET /api/v1/hotels/{id}</c> — erişilemeyen otel 404 döner.</summary>
public sealed record GetHotelByIdRequest(Guid Id) : IRequest<HotelResponse>;
