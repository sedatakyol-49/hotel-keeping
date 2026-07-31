using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.GetHold;

/// <summary>
/// Donmuş teklifi okur.
/// <para>
/// <b>Başka otelin token'ı → 404:</b> ayrı bir kontrol yazılmaz. Slug yolda olduğu için tenant
/// kapsamı sorgudan <i>önce</i> kurulur; başka otelin hold satırı global query filter'a takılır
/// ve hiç bulunamaz. Bu, "önce token'ı filtresiz bul, otelini öğren, sonra kapsamı kur"
/// tasarımına göre bilinçli bir tercihtir — o tasarım public yola tek bir filtre bypass'ı
/// sokardı.
/// </para>
/// </summary>
internal sealed class PublicGetHoldHandler(
    IAppDbContext database,
    PublicHotelReader hotels,
    PublicHoldService holds)
    : IRequestHandler<PublicGetHoldRequest, PublicHoldResponse>
{
    public async Task<PublicHoldResponse> Handle(
        PublicGetHoldRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);

        if (!PublicTokens.IsWellFormedUrlToken(request.HoldToken, PublicTokens.HoldTokenLength))
        {
            // Biçimi bozuk token için veritabanına hiç gidilmez; yanıt yine 404'tür (varlık
            // sızdırılmaz), ama gereksiz bir sorgu da yapılmaz.
            throw PublicApiException.NotFound(PublicErrorCodes.HoldNotFound, Messages.PublicHoldNotFound);
        }

        var hold = await holds.FindAsync(request.HoldToken, cancellationToken).ConfigureAwait(false);
        holds.EnsureUsable(hold);

        var roomTypeCode = await database.RoomTypes
            .AsNoTracking()
            .Where(roomType => roomType.Id == hold!.RoomTypeId)
            .Select(roomType => roomType.Code)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        // Ham token yanıtta TEKRAR EDİLMEZ: istemci zaten ona sahiptir; yanıtın loglanması
        // hâlinde ikinci bir sızıntı yolu açmanın faydası yoktur.
        return holds.BuildFromSnapshot(context, hold!, roomTypeCode, rawToken: request.HoldToken);
    }
}
