using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.GetById;

/// <summary>
/// <c>GET /api/v1/guests/{id}</c> — başka otelin kaydı 404 döner. Yanıtta geçmiş konaklama
/// sayısı (<c>stayCount</c>) sunucuda hesaplanır.
/// </summary>
public sealed record GetGuestByIdRequest(Guid Id) : IRequest<GuestResponse>;
