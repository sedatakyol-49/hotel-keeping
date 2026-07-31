using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Public.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.ListBrandHotels;

/// <summary>
/// Marka otel listesi.
///
/// <para><b>Bu uç neden özel:</b> yanıt birden çok otelin kapak görselini taşır, ama
/// <c>HotelImage</c> tenant-scoped'dır ve tek bir <c>HotelId</c> ile birden çok otelin görseli
/// okunamaz. İki kolay çıkış yolu vardı ve <b>ikisi de reddedildi</b>:
/// <c>IgnoreQueryFilters()</c> (public yolda yasak) ve <c>CanAccessAllHotels = true</c> (public
/// kanalın değişmezini bozar). Bunun yerine kapsam otelden otele <b>daraltılır</b>
/// (<see cref="PublicTenantScope.Enter"/>): her görsel yalnızca kendi otelinin kapsamı
/// yürürlükteyken okunur. İzolasyon tam korunur; bedeli otel sayısı kadar küçük sorgudur.</para>
///
/// <para><b>404 <c>BRAND_NOT_FOUND</c>:</b> slug yok <b>veya</b> markanın public kanalı açık
/// hiçbir oteli yok — iki durum ayırt edilmez (varlık sızdırılmaz).</para>
/// </summary>
internal sealed class PublicListBrandHotelsHandler(
    IAppDbContext database,
    PublicTenantScope tenantScope,
    PublicContentReader content)
    : IRequestHandler<PublicListBrandHotelsRequest, IReadOnlyList<PublicHotelListItemResponse>>
{
    public async Task<IReadOnlyList<PublicHotelListItemResponse>> Handle(
        PublicListBrandHotelsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slug = request.BrandSlug.Trim().ToLowerInvariant();

        var hotels = await database.Hotels
            .AsNoTracking()
            .Where(hotel => hotel.HeadOffice.PublicSlug == slug
                            && hotel.PublicSlug != null
                            && hotel.PublicBookingSettings.IsEnabled)
            .OrderBy(hotel => hotel.Name)
            .Select(hotel => new
            {
                hotel.Id,
                Slug = hotel.PublicSlug!,
                hotel.Name,
                hotel.City,
                hotel.Country,
                hotel.Currency,
                hotel.DefaultCulture
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (hotels.Count == 0)
        {
            throw PublicApiException.NotFound(PublicErrorCodes.BrandNotFound, Messages.PublicBrandNotFound);
        }

        var culture = RequestCulture.Current;
        var result = new List<PublicHotelListItemResponse>(hotels.Count);

        foreach (var hotel in hotels)
        {
            // Kapsam yalnızca bu otele daraltılır: sonraki iki sorgu başka otelin satırını
            // fiziksel olarak göremez.
            using (tenantScope.Enter(hotel.Id))
            {
                var images = await content.GetHotelImagesAsync(culture, cancellationToken).ConfigureAwait(false);
                var description = await content
                    .GetHotelDescriptionAsync(hotel.Id, culture, cancellationToken)
                    .ConfigureAwait(false);

                result.Add(new PublicHotelListItemResponse
                {
                    Slug = hotel.Slug,
                    Name = hotel.Name,
                    City = hotel.City,
                    Country = hotel.Country.ToString(),
                    Currency = hotel.Currency,
                    DefaultCulture = hotel.DefaultCulture,
                    ShortDescription = PublicContentReader.ShortDescription(description),

                    // Kapak = en küçük sortOrder (liste zaten sıralı gelir).
                    Image = images.Count > 0 ? images[0] : null
                });
            }
        }

        return result;
    }
}
