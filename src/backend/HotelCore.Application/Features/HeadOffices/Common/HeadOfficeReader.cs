using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.HeadOffices.Common;

/// <summary>
/// Head Office okuma yolu.
/// <para>
/// Kullanıcı <b>yalnızca kendi</b> Head Office'ini görebilir: kimlik JWT'deki
/// <c>headOfficeId</c> claim'inden gelir, istekten alınmaz — böylece başka markanın
/// ayarlarına erişim yolu hiç açılmaz.
/// </para>
/// </summary>
internal sealed class HeadOfficeReader(IAppDbContext database, ICurrentUser currentUser)
{
    public async Task<HeadOfficeSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var id = RequireHeadOfficeId();

        var settings = await database.HeadOffices
            .Where(headOffice => headOffice.Id == id)
            .Select(headOffice => new HeadOfficeSettingsResponse
            {
                Id = headOffice.Id,
                BrandName = headOffice.BrandName,
                DefaultCulture = headOffice.DefaultCulture,
                HotelCount = headOffice.Hotels.Count(hotel => !hotel.IsDeleted),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return settings ?? throw new NotFoundException("Head Office bulunamadi.");
    }

    public async Task<HeadOffice> GetTrackedAsync(CancellationToken cancellationToken)
    {
        var id = RequireHeadOfficeId();

        var headOffice = await database.HeadOffices
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return headOffice ?? throw new NotFoundException("Head Office bulunamadi.");
    }

    /// <summary>
    /// Kimlikte <c>headOfficeId</c> yoksa istek işlenemez. Bu bir doğrulama hatası değil
    /// yetki bağlamı eksikliğidir; 403 döner.
    /// </summary>
    private Guid RequireHeadOfficeId() =>
        currentUser.HeadOfficeId
        ?? throw new ForbiddenException("Kimlikte bagli bir Head Office bulunamadi.");
}
