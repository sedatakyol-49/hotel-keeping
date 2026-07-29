using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Auth.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Auth.Login;

/// <summary>
/// E-posta + parola ile oturum açar; access token, rotating refresh token ve
/// kullanıcı profilini döner.
/// <para>
/// Güvenlik: kullanıcı bulunamadı / parola hatalı / hesap pasif durumlarının hepsinde
/// aynı <see cref="AuthenticationException"/> fırlatılır (user enumeration engellenir).
/// </para>
/// </summary>
internal sealed class LoginHandler(
    IAppDbContext database,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    AuthSessionService sessionService)
    : IRequestHandler<LoginRequest, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // E-posta küçük harfe normalize edilerek saklanır (User entity sözleşmesi),
        // böylece benzersiz indeks doğrudan kullanılabilir.
        var email = request.Email.Trim().ToLowerInvariant();

        // User tenant-scoped değildir; yalnızca soft-delete filtresi uygulanır.
        var user = await database.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationException();
        }

        var profile = await sessionService.BuildProfileAsync(user, cancellationToken).ConfigureAwait(false);
        var (tokens, _) = sessionService.IssueTokens(user, profile, request.IpAddress);

        user.LastLoginAt = dateTimeProvider.UtcNow;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresAtUtc,
            tokens.TokenType,
            profile);
    }
}
