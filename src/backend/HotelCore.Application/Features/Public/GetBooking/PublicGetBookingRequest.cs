using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetBooking;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}/bookings/{accessToken}</c> — rezervasyon sorgulama.
/// <para>
/// Yanıt <c>accessToken</c> alanını <b>taşımaz</b>: istemci zaten ona sahiptir ve yanıtın
/// loglanması/paylaşılması hâlinde taşıyıcı kimlik bilgisinin ikinci kez sızmasının hiçbir
/// faydası yoktur.
/// </para>
/// </summary>
/// <param name="AccessToken">Ham erişim token'ı (base64url, 27 karakter).</param>
public sealed record PublicGetBookingRequest(string AccessToken) : IRequest<PublicBookingResponse>;
