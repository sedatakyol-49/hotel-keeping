using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.ListRoomTypes;

/// <summary>Oda tipi kataloğu (prerender edilen sayfaların kaynağı).</summary>
internal sealed class PublicListRoomTypesHandler(PublicHotelReader hotels, PublicContentReader content)
    : IRequestHandler<PublicListRoomTypesRequest, IReadOnlyList<PublicRoomTypeSummaryResponse>>
{
    public async Task<IReadOnlyList<PublicRoomTypeSummaryResponse>> Handle(
        PublicListRoomTypesRequest request,
        CancellationToken cancellationToken)
    {
        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var culture = RequestCulture.Current;

        var roomTypes = await content.ListRoomTypesAsync(culture, cancellationToken).ConfigureAwait(false);
        var images = await content
            .GetRoomTypeImagesAsync(roomTypes.Select(row => row.Id).ToArray(), culture, cancellationToken)
            .ConfigureAwait(false);

        return roomTypes.Select(row => new PublicRoomTypeSummaryResponse
        {
            Code = row.Code,
            Name = row.Name,
            ShortDescription = PublicContentReader.ShortDescription(row.Description),
            Capacity = row.Capacity,
            SizeSqm = row.SizeSqm,
            Amenities = PublicContentReader.Amenities(row.Amenities),
            Image = images.TryGetValue(row.Id, out var list) && list.Count > 0 ? list[0] : null,
            FromPrice = new PublicFromPriceResponse
            {
                Amount = row.BasePrice,
                Currency = context.Hotel.Currency,

                // Tarihsiz katalogda sezon fiyatı gösterilemez; PAngV açısından bu bir
                // "ab" fiyatıdır ve toplam fiyat iddiası DEĞİLDİR.
                Basis = "BasePrice"
            }
        }).ToArray();
    }
}
