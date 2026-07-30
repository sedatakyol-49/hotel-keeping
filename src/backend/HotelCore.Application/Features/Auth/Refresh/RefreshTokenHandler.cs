using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Auth.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotelCore.Application.Features.Auth.Refresh;

/// <summary>
/// Rotating refresh token akışı:
/// <list type="number">
///   <item>Gelen ham token'ın SHA-256 özetiyle kayıt bulunur (ham token DB'de tutulmaz).</item>
///   <item>Kayıt yoksa veya süresi geçmişse → 401.</item>
///   <item>Kayıt <b>zaten iptal edilmişse</b> (yeniden kullanım) → kullanıcının tüm aktif
///         token'ları iptal edilir ve 401 döner (çalınmış token zinciri kapatılır).</item>
///   <item>Aksi hâlde eski token iptal edilip yenisine bağlanır, yeni çift üretilir.</item>
/// </list>
/// Yanıt yalnızca token çiftidir; kullanıcı nesnesi içermez (sözleşme).
/// </summary>
internal sealed class RefreshTokenHandler(
    IAppDbContext database,
    IJwtTokenService tokenService,
    IDateTimeProvider dateTimeProvider,
    AuthSessionService sessionService,
    ILogger<RefreshTokenHandler> logger)
    : IRequestHandler<RefreshTokenRequest, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var now = dateTimeProvider.UtcNow;

        var stored = await database.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            throw new AuthenticationException(InvalidTokenMessage);
        }

        if (stored.RevokedAt is not null)
        {
            // Yeniden kullanım: token sızmış olabilir → tüm aktif oturumlar kapatılır.
            await RevokeAllActiveTokensAsync(stored.UserId, now, request.IpAddress, cancellationToken)
                .ConfigureAwait(false);

            logger.RefreshTokenReuseDetected(stored.UserId);

            throw new AuthenticationException(InvalidTokenMessage);
        }

        if (stored.ExpiresAt <= now)
        {
            throw new AuthenticationException(InvalidTokenMessage);
        }

        // Include, User üzerindeki soft-delete filtresine tabidir: silinmiş kullanıcıda null gelir.
        var user = stored.User;
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationException(InvalidTokenMessage);
        }

        var profile = await sessionService.BuildProfileAsync(user, cancellationToken).ConfigureAwait(false);
        var (tokens, replacement) = sessionService.IssueTokens(user, profile, request.IpAddress);

        stored.RevokedAt = now;
        stored.RevokedByIp = request.IpAddress;
        stored.ReplacedByTokenId = replacement.Id;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return tokens;
    }

    /// <summary>Kullanıcının süresi dolmamış ve iptal edilmemiş tüm token'larını iptal eder.</summary>
    private async Task RevokeAllActiveTokensAsync(
        Guid userId,
        DateTimeOffset now,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        // IsActive mapped değildir — koşul açıkça yazılır.
        var activeTokens = await database.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ipAddress;
        }

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tüm geçersiz token senaryolarında aynı mesaj kullanılır (bilgi sızdırmamak için).</summary>
    private static string InvalidTokenMessage => Messages.InvalidRefreshToken;
}
