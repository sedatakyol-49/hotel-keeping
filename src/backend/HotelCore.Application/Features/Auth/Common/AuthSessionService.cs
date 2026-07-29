using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Auth.Common;

/// <summary>
/// Login ve refresh use-case'lerinin ortak gövdesi: kullanıcı profilinin (rol + izin + otel)
/// okunması ve token çiftinin üretilmesi. İki handler'da tekrarlanmaması için tek yerde toplandı.
/// </summary>
internal sealed class AuthSessionService(
    IAppDbContext database,
    IJwtTokenService tokenService,
    IDateTimeProvider dateTimeProvider,
    IMapper mapper)
{
    /// <summary>Frontend sözleşmesindeki <c>user</c> nesnesini kurar.</summary>
    public async Task<UserProfileDto> BuildProfileAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Rol satırları: hem ad listesi hem de "tüm oteller" bypass'ı buradan gelir.
        var roleRows = await database.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => new { ur.RoleId, ur.Role.Name, ur.Role.IsHeadOfficeLevel })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var canAccessAllHotels = roleRows.Exists(r => r.IsHeadOfficeLevel);
        var roleIds = roleRows.ConvertAll(r => r.RoleId);

        var permissions = await database.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .OrderBy(key => key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // UserHotelAccess tenant filtresine tabi değildir; aktif otel seçilmeden okunabilir.
        var accesses = await database.UserHotelAccesses
            .Where(a => a.UserId == user.Id)
            .Select(a => new { a.HotelId, a.IsDefault })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var accessibleHotelIds = accesses.ConvertAll(a => a.HotelId);

        // Head Office kullanıcısı kendi head office'inin TÜM otellerini görür (konsolide görünüm).
        var hotelsQuery = canAccessAllHotels
            ? database.Hotels.Where(h => h.HeadOfficeId == user.HeadOfficeId)
            : database.Hotels.Where(h => accessibleHotelIds.Contains(h.Id));

        var hotels = await hotelsQuery
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var defaultHotelId =
            accesses.Find(a => a.IsDefault)?.HotelId
            ?? (hotels.Count > 0 ? hotels[0].Id : (Guid?)null);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = null,
            Culture = user.Culture,
            HeadOfficeId = user.HeadOfficeId,
            Roles = roleRows.ConvertAll(r => r.Name).Order(StringComparer.Ordinal).ToList(),
            Permissions = permissions,
            Hotels = mapper.Map<List<HotelSummaryDto>>(hotels),
            CanAccessAllHotels = canAccessAllHotels,
            DefaultHotelId = defaultHotelId
        };
    }

    /// <summary>
    /// Access token'ı üretir ve yeni refresh token'ı <b>takibe ekler</b> (SaveChanges ÇAĞIRMAZ).
    /// Kaydetme sorumluluğu handler'dadır; böylece rotation tek transaction'da yapılabilir.
    /// </summary>
    public (AuthTokensDto Tokens, RefreshToken Entity) IssueTokens(
        User user,
        UserProfileDto profile,
        string? ipAddress)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(profile);

        // Sıra anlamlı: varsayılan otel başa alınır — X-Hotel-Id yokken ilk "hotel" claim'i aktif oteldir.
        var hotelIds = profile.Hotels
            .Select(h => h.Id)
            .OrderByDescending(id => profile.DefaultHotelId == id)
            .ToList();

        var accessToken = tokenService.CreateAccessToken(new AccessTokenDescriptor(
            user.Id,
            user.Email,
            user.HeadOfficeId,
            user.Culture,
            profile.Permissions,
            hotelIds,
            profile.CanAccessAllHotels));

        var refreshToken = tokenService.CreateRefreshToken();

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt,
            CreatedAt = dateTimeProvider.UtcNow,
            CreatedByIp = ipAddress
        };

        database.RefreshTokens.Add(entity);

        return (
            new AuthTokensDto(accessToken.Value, refreshToken.RawValue, accessToken.ExpiresAtUtc, "Bearer"),
            entity);
    }
}
