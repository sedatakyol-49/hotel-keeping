namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Access token üretimi için gereken kimlik bağlamı. Alanlar api-contracts.md
/// "JWT Claim Şeması" ile birebir eşleşir.
/// </summary>
/// <param name="UserId"><c>sub</c> claim'i.</param>
/// <param name="Email"><c>email</c> claim'i.</param>
/// <param name="HeadOfficeId"><c>headOfficeId</c> claim'i.</param>
/// <param name="Culture"><c>culture</c> claim'i.</param>
/// <param name="Permissions">Çoklu <c>perm</c> claim'i.</param>
/// <param name="HotelIds">
/// Çoklu <c>hotel</c> claim'i. <b>Sıra anlamlıdır:</b> ilk eleman kullanıcının varsayılan
/// otelidir (X-Hotel-Id header'ı gelmediğinde aktif otel olarak kullanılır).
/// </param>
/// <param name="CanAccessAllHotels"><c>allHotels</c> claim'i — Head Office bypass.</param>
public sealed record AccessTokenDescriptor(
    Guid UserId,
    string Email,
    Guid HeadOfficeId,
    string Culture,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> HotelIds,
    bool CanAccessAllHotels);

/// <summary>Üretilen access token ve UTC geçerlilik sonu.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>
/// Üretilen refresh token. <see cref="RawValue"/> yalnızca istemciye döner;
/// veritabanına <see cref="TokenHash"/> yazılır.
/// </summary>
public sealed record RefreshTokenResult(string RawValue, string TokenHash, DateTimeOffset ExpiresAt);

/// <summary>Token üretimi/özetleme portu. Implementasyon: Infrastructure/Security.</summary>
public interface IJwtTokenService
{
    AccessToken CreateAccessToken(AccessTokenDescriptor descriptor);

    RefreshTokenResult CreateRefreshToken();

    /// <summary>Ham refresh token'ın DB'de aranacak SHA-256 özetini üretir.</summary>
    string HashRefreshToken(string rawToken);
}
