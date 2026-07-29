using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Auth.Common;

namespace HotelCore.Application.Features.Auth.Refresh;

/// <summary>
/// <c>POST /api/v1/auth/refresh</c> gövdesi.
/// </summary>
/// <param name="RefreshToken">Login veya önceki refresh çağrısından alınan ham token.</param>
public sealed record RefreshTokenRequest(string RefreshToken) : IRequest<AuthTokensDto>
{
    /// <summary>İstemci IP'si — controller doldurur, istek gövdesinden okunmaz.</summary>
    [JsonIgnore]
    public string? IpAddress { get; init; }
}
