namespace HotelCore.Application.Features.Auth.Common;

/// <summary>
/// <c>POST /api/v1/auth/refresh</c> yanıtı — yalnızca token çifti, kullanıcı nesnesi YOKTUR.
/// <para>
/// <see cref="ExpiresAtUtc"/> bilinçli olarak <see cref="DateTime"/>'dır (Kind = Utc):
/// System.Text.Json bunu <c>"...Z"</c> olarak serileştirir; DateTimeOffset ise <c>"+00:00"</c>
/// üretirdi ve sözleşmedeki örnekten sapardı.
/// </para>
/// </summary>
public sealed record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string TokenType);
