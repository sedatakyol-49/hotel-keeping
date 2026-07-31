using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Public.ReleaseHold;

/// <summary>
/// <c>DELETE /api/v1/public/hotels/{hotelSlug}/holds/{holdToken}</c> — misafir akıştan çıkarsa
/// envanteri <b>hemen</b> serbest bırakır.
/// <para>
/// <b>İdempotenttir:</b> bilinmeyen, süresi dolmuş veya başka otele ait token da <c>204</c>
/// döner. Farklı yanıt vermek, token'ın var olup olmadığını sızdırırdı.
/// </para>
/// </summary>
/// <param name="HoldToken">Ham hold token'ı.</param>
public sealed record PublicReleaseHoldRequest(string HoldToken) : IRequest<Unit>;
