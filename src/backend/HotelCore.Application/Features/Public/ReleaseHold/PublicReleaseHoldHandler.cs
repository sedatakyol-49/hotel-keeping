using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.ReleaseHold;

/// <summary>
/// Hold'u bırakır.
/// <para>
/// <b>Tüketilmiş hold SİLİNMEZ:</b> satır artık envanteri bloke etmiyordur (odayı rezervasyonun
/// kendisi tutar) ve <c>ConsumedByReservationId</c> destek için tek geri izdir. Süpürücü onu 24
/// saat sonra zaten temizler.
/// </para>
/// </summary>
internal sealed class PublicReleaseHoldHandler(PublicHotelReader hotels, PublicHoldService holds)
    : IRequestHandler<PublicReleaseHoldRequest, Unit>
{
    public async Task<Unit> Handle(PublicReleaseHoldRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Otel kapsamı yine de doğrulanır: kapalı kanalda hiçbir uç çalışmaz (404).
        await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);

        if (!PublicTokens.IsWellFormedUrlToken(request.HoldToken, PublicTokens.HoldTokenLength))
        {
            return Unit.Value;
        }

        var hold = await holds.FindAsync(request.HoldToken, cancellationToken).ConfigureAwait(false);

        if (hold is not null && hold.ConsumedAt is null)
        {
            await holds.ReleaseAsync(hold, cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
