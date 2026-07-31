using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Options;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.CreateHold;

/// <summary>
/// Teklifi dondurur ve somut bir odayı 15 dakika tutar.
///
/// <para><b>Neden hold var</b> (architecture-public-booking.md §5.2) — üç bağımsız sebep:
/// (1) §312j Abs. 2 BGB'nin zorunlu özeti <i>gerçekten ödenecek</i> tutarı göstermek zorundadır;
/// fiyat/uygunluk özet ile buton arasında değişirse gösterilen özet yanlıştır. (2) Misafir
/// "zahlungspflichtig buchen"e bastıktan <i>sonra</i> "son oda satıldı" demek, iradenin beyan
/// edildiği anda sözleşmenin kurulmaması demektir. (3) <c>Reservation.RoomId</c> zorunlu olduğu
/// için somut oda seçimi ertelenemez; pinlenmeden iki eşzamanlı istek aynı odayı seçerdi.</para>
///
/// <para><b>Ama hold tek başına yetmez.</b> Kalan yarış pencerelerini (tam süre dolarken gelen
/// istek, resepsiyonun aynı odayı elle satması, süpürücü gecikmesi) <c>Reservations</c>
/// üzerindeki dışlama kısıtı kapatır. Hold, admin yazmalarına karşı yalnızca <b>tavsiye
/// niteliğindedir</b>: iki <c>EXCLUDE</c> kısıtı farklı tablolardadır ve veritabanı "hold'lu
/// odaya resepsiyon rezervasyonu" durumunu engellemez. Misafir bunu tek bir olay olarak görür:
/// <c>409 ROOM_NO_LONGER_AVAILABLE</c>.</para>
/// </summary>
internal sealed class PublicCreateHoldHandler(
    PublicHotelReader hotels,
    PublicContentReader content,
    PublicHoldService holds,
    PublicLegalReader legal,
    IDateTimeProvider clock,
    PublicChannelOptions options)
    : IRequestHandler<PublicCreateHoldRequest, PublicHoldResponse>
{
    public async Task<PublicHoldResponse> Handle(
        PublicCreateHoldRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var culture = RequestCulture.Current;

        PublicStayRules.ValidateHold(
            context.Hotel,
            context.LocalToday(now),
            now,
            context.TimeZone,
            request.CheckIn,
            request.CheckOut,
            request.Adults,
            request.Children);

        var roomType = await content.FindRoomTypeAsync(request.RoomTypeCode, culture, cancellationToken)
                           .ConfigureAwait(false)
                       ?? throw PublicApiException.NotFound(
                           PublicErrorCodes.RoomTypeNotFound,
                           Messages.PublicRoomTypeNotFound);

        PublicStayRules.EnsureCapacity(roomType.Capacity, request.Adults, request.Children);

        var versions = await legal
            .GetActiveVersionsAsync(culture, context.Hotel.DefaultCulture, cancellationToken)
            .ConfigureAwait(false);

        var creation = await holds
            .CreateAsync(
                context,
                roomType,
                request.CheckIn,
                request.CheckOut,
                request.Adults,
                request.Children,
                culture,
                PublicTokens.HashClientIp(request.ClientIp, options.ClientIpHashSalt),
                versions,
                cancellationToken)
            .ConfigureAwait(false);

        return creation.Response;
    }
}
