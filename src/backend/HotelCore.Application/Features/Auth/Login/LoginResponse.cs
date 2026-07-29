using HotelCore.Application.Features.Auth.Common;

namespace HotelCore.Application.Features.Auth.Login;

/// <summary>
/// <c>POST /api/v1/auth/login</c> yanıtı. Frontend bu düz şekle bağlıdır:
/// token alanları kök seviyede, kullanıcı profili <c>user</c> altında.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string TokenType,
    UserProfileDto User);
