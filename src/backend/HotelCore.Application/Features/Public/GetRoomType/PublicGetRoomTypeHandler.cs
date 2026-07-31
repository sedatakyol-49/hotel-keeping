using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetRoomType;

/// <summary>
/// Oda tipi detayı — SEO'nun asıl hedef sayfası.
/// <para>
/// <b>404 <c>ROOM_TYPE_NOT_FOUND</c>:</b> kod yok <b>veya</b> oda tipi başka otele ait. İkinci
/// durum için ayrı bir kontrol yazılmaz: global query filter aktif otelin dışındaki satırı zaten
/// göstermez, dolayısıyla "başka otelin kodu" ile "olmayan kod" aynı sonucu verir.
/// </para>
/// </summary>
internal sealed class PublicGetRoomTypeHandler(PublicHotelReader hotels, PublicContentReader content)
    : IRequestHandler<PublicGetRoomTypeRequest, PublicRoomTypeDetailResponse>
{
    public async Task<PublicRoomTypeDetailResponse> Handle(
        PublicGetRoomTypeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var culture = RequestCulture.Current;

        var row = await content.FindRoomTypeAsync(request.RoomTypeCode, culture, cancellationToken)
                      .ConfigureAwait(false)
                  ?? throw PublicApiException.NotFound(
                      PublicErrorCodes.RoomTypeNotFound,
                      Messages.PublicRoomTypeNotFound);

        var images = await content
            .GetRoomTypeImagesAsync([row.Id], culture, cancellationToken)
            .ConfigureAwait(false);

        var policy = context.Hotel.CancellationPolicy;

        return new PublicRoomTypeDetailResponse
        {
            Code = row.Code,
            Name = row.Name,
            ShortDescription = PublicContentReader.ShortDescription(row.Description),
            Description = row.Description,
            Capacity = row.Capacity,
            SizeSqm = row.SizeSqm,
            Amenities = PublicContentReader.Amenities(row.Amenities),
            Images = images.TryGetValue(row.Id, out var list) ? list : [],
            FromPrice = new PublicFromPriceResponse
            {
                Amount = row.BasePrice,
                Currency = context.Hotel.Currency,
                Basis = "BasePrice"
            },
            CancellationPolicy = new PublicHotelCancellationPolicyResponse
            {
                Type = policy.Type.ToString(),
                FreeCancellationDaysBeforeArrival = policy.FreeCancellationDaysBeforeArrival,
                CutoffLocalTime = policy.CutoffLocalTime,
                LateCancellationFeePercent = policy.LateCancellationFeePercent,
                NoShowFeePercent = policy.NoShowFeePercent,
                AppliesToAccommodationOnly = true
            }
        };
    }
}
