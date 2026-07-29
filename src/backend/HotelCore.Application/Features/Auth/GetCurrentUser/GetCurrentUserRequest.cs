using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Auth.Common;

namespace HotelCore.Application.Features.Auth.GetCurrentUser;

/// <summary>
/// <c>GET /api/v1/auth/me</c> — parametresizdir; kimlik <c>ICurrentUser</c>'dan (JWT) okunur.
/// </summary>
public sealed record GetCurrentUserRequest : IRequest<UserProfileDto>;
