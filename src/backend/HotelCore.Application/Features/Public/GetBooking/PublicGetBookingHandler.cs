using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetBooking;

/// <summary>
/// Rezervasyon sorgulama.
/// <para>
/// <b>Başka otelin token'ı da 404 verir</b> ve bunun için ayrı bir kontrol yoktur: slug yolda
/// olduğu için tenant kapsamı sorgudan önce kurulur ve satır global query filter'a takılır.
/// "Token yok", "süresi dolmuş" ve "başka otelin" üç durumu <b>ayırt edilmez</b> — gövde de
/// zamanlama da fark etmez.
/// </para>
/// </summary>
internal sealed class PublicGetBookingHandler(PublicHotelReader hotels, PublicBookingReader bookings)
    : IRequestHandler<PublicGetBookingRequest, PublicBookingResponse>
{
    public async Task<PublicBookingResponse> Handle(
        PublicGetBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var row = await bookings.RequireByAccessTokenAsync(request.AccessToken, cancellationToken)
            .ConfigureAwait(false);

        return bookings.BuildResponse(context, row, rawAccessToken: null);
    }
}
