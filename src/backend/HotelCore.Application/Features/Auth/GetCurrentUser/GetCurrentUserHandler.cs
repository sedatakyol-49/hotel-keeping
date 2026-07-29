using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Auth.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Auth.GetCurrentUser;

/// <summary>
/// Aktif kullanıcının profilini (rol + izin + erişilebilir oteller) döner.
/// Yanıt, login yanıtındaki <c>user</c> nesnesinin birebir aynısıdır.
/// <para>
/// İzinler her istekte veritabanından okunur; böylece rol değişikliği token'ın süresi
/// dolmadan da <c>/me</c> üzerinden görünür olur.
/// </para>
/// </summary>
internal sealed class GetCurrentUserHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    AuthSessionService sessionService)
    : IRequestHandler<GetCurrentUserRequest, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetCurrentUserRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
        {
            throw new AuthenticationException("Kimlik dogrulanmamis.");
        }

        var user = await database.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        // Token geçerli ama kullanıcı silinmiş/pasif ise oturum artık geçersizdir.
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationException("Kullanici bulunamadi veya pasif.");
        }

        return await sessionService.BuildProfileAsync(user, cancellationToken).ConfigureAwait(false);
    }
}
