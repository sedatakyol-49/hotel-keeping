using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HotelCore.Infrastructure.Security;

/// <summary>
/// JWT access token üretimi ve refresh token üretimi/özetlemesi.
/// <para>
/// Claim şeması api-contracts.md ile birebirdir: <c>sub</c>, <c>email</c>, <c>headOfficeId</c>,
/// çoklu <c>perm</c>, çoklu <c>hotel</c>, <c>allHotels</c>, <c>culture</c>.
/// Standart <c>iss/aud/exp/iat/nbf/jti</c> alanları ayrıca yazılır.
/// </para>
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _options.Validate();

        _dateTimeProvider = dateTimeProvider;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(AccessTokenDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var issuedAt = _dateTimeProvider.UtcNow.UtcDateTime;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>(capacity: 6 + descriptor.Permissions.Count + descriptor.HotelIds.Count)
        {
            new(JwtClaimNames.Subject, descriptor.UserId.ToString()),
            new(JwtClaimNames.Email, descriptor.Email),
            new(JwtClaimNames.HeadOfficeId, descriptor.HeadOfficeId.ToString()),
            new(JwtClaimNames.Culture, descriptor.Culture),
            // Sözleşme gereği METİN "true"/"false" (api-contracts.md): boolean claim tipi
            // kullanılsaydı JWT'de JSON boolean üretilirdi ve şemadan sapardı.
            new(JwtClaimNames.AllHotels, descriptor.CanAccessAllHotels ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Çoklu claim: aynı tipte birden fazla değer JWT'de dizi olarak serileştirilir.
        claims.AddRange(descriptor.Permissions.Select(permission => new Claim(JwtClaimNames.Permission, permission)));

        // Sıra korunur: ilk "hotel" claim'i kullanıcının varsayılan otelidir.
        claims.AddRange(descriptor.HotelIds.Select(hotelId => new Claim(JwtClaimNames.Hotel, hotelId.ToString())));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials
        };

        return new AccessToken(_tokenHandler.CreateToken(tokenDescriptor), expiresAt);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        // 512 bit kriptografik rastgelelik — tahmin edilemezlik için fazlasıyla yeterli.
        var bytes = RandomNumberGenerator.GetBytes(64);
        var rawValue = Base64UrlEncoder.Encode(bytes);

        return new RefreshTokenResult(
            rawValue,
            HashRefreshToken(rawValue),
            _dateTimeProvider.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        // Token yüksek entropili rastgele bir değer olduğu için düz SHA-256 yeterlidir
        // (parolalardan farklı olarak sözlük saldırısına açık değildir).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
