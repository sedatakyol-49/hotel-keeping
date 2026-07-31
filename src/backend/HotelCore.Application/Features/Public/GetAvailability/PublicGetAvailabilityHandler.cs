using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetAvailability;

/// <summary>
/// Müsaitlik araması ve fiyat teklifi.
///
/// <para><b>Doluluk ifşa edilmez:</b> müsait oda sayısı <b>5'te kırpılır</b>. Kırpma doğruluğu
/// bozmaz (UWG §5 yanıltıcı kıtlık iddiası yasağı): gösterilen sayı gerçek bir alt sınırdır,
/// uydurulmuş bir sayı değildir.</para>
///
/// <para><b>Boş sonuç hata değildir:</b> hiçbir tip müsait değilse <c>offers: []</c> ile
/// <b>200</b> döner. 404 dönmek "otel yok" ile "oda yok" ayrımını kaybettirirdi.</para>
///
/// <para><b>Neden gerekçe döndürülüyor:</b> misafire "başka bir tarih/kişi sayısı deneyin"
/// demenin tek doğru yolu hangi kısıtın engellediğini bilmektir. Gerekçe <b>sayı vermez</b>.</para>
/// </summary>
internal sealed class PublicGetAvailabilityHandler(
    PublicHotelReader hotels,
    PublicContentReader content,
    PublicAvailabilityReader availability,
    PublicPricingService pricing,
    IDateTimeProvider clock)
    : IRequestHandler<PublicGetAvailabilityRequest, PublicAvailabilityResponse>
{
    public async Task<PublicAvailabilityResponse> Handle(
        PublicGetAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var hotelToday = context.LocalToday(now);

        PublicStayRules.ValidateSearch(
            context.Hotel,
            hotelToday,
            request.CheckIn,
            request.CheckOut,
            request.Adults,
            request.Children);

        var culture = RequestCulture.Current;
        var nights = request.CheckOut.DayNumber - request.CheckIn.DayNumber;
        var minNights = context.Hotel.PublicBookingSettings.MinNights;

        var roomTypes = await content.ListRoomTypesAsync(culture, cancellationToken).ConfigureAwait(false);
        var images = await content
            .GetRoomTypeImagesAsync(roomTypes.Select(row => row.Id).ToArray(), culture, cancellationToken)
            .ConfigureAwait(false);

        // Tüm oda tipleri için müsait odalar TEK sorguda alınır (tip başına sorgu = N+1).
        var availableRooms = await availability
            .GetAvailableRoomsAsync(request.CheckIn, request.CheckOut, now, roomTypeId: null, cancellationToken)
            .ConfigureAwait(false);

        var countsByType = availableRooms
            .GroupBy(room => room.RoomTypeId)
            .ToDictionary(group => group.Key, group => group.Count());

        var offers = new List<PublicOfferResponse>();
        var unavailable = new List<PublicUnavailableRoomTypeResponse>();

        foreach (var roomType in roomTypes)
        {
            var reason = ResolveUnavailability(roomType, request, nights, minNights, countsByType);
            if (reason is not null)
            {
                unavailable.Add(new PublicUnavailableRoomTypeResponse
                {
                    RoomTypeCode = roomType.Code,
                    Name = roomType.Name,
                    Reason = reason
                });

                continue;
            }

            var price = await pricing
                .BuildAsync(
                    context,
                    roomType.Id,
                    roomType.BasePrice,
                    request.CheckIn,
                    request.CheckOut,
                    request.Adults,
                    request.Children,
                    cancellationToken)
                .ConfigureAwait(false);

            var (units, capped) = PublicAvailabilityReader.Cap(countsByType[roomType.Id]);

            offers.Add(new PublicOfferResponse
            {
                RoomTypeCode = roomType.Code,
                Name = roomType.Name,
                ShortDescription = PublicContentReader.ShortDescription(roomType.Description),
                Capacity = roomType.Capacity,
                SizeSqm = roomType.SizeSqm,
                Amenities = PublicContentReader.Amenities(roomType.Amenities),
                Image = images.TryGetValue(roomType.Id, out var list) && list.Count > 0 ? list[0] : null,
                Availability = new PublicOfferAvailabilityResponse
                {
                    IsAvailable = true,
                    AvailableUnits = units,
                    AvailableUnitsCapped = capped
                },
                Price = price,
                CancellationPolicy = PublicCancellationService.Build(
                    context,
                    request.CheckIn,
                    price.AccommodationGross,
                    now)
            });
        }

        return new PublicAvailabilityResponse
        {
            HotelSlug = context.Hotel.PublicSlug!,
            Currency = context.Hotel.Currency,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            Nights = nights,
            Adults = request.Adults,
            Children = request.Children,
            Offers = offers,
            UnavailableRoomTypes = unavailable
        };
    }

    /// <summary>
    /// Gerekçe sırası anlamlıdır: kapasite ve minimum gece <b>istekten</b> doğar, "oda yok" ise
    /// envanterin durumudur. Misafire önce düzeltebileceği kısıt söylenir.
    /// </summary>
    private static string? ResolveUnavailability(
        PublicRoomTypeRow roomType,
        PublicGetAvailabilityRequest request,
        int nights,
        int minNights,
        Dictionary<Guid, int> countsByType)
    {
        if (request.Adults + request.Children > roomType.Capacity)
        {
            return "CapacityExceeded";
        }

        if (nights < minNights)
        {
            return "MinNightsNotMet";
        }

        return countsByType.TryGetValue(roomType.Id, out var count) && count > 0
            ? null
            : "NoRoomAvailable";
    }
}
