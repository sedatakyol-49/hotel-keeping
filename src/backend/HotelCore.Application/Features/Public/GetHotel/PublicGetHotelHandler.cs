using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetHotel;

/// <summary>
/// Otel künyesi. Tüm değerler <b>veritabanından</b> gelir: marka adı, iptal politikası, Kurtaxe
/// künyesi ve rezervasyon sınırları koda gömülü değildir (architecture.md §4.1).
/// </summary>
internal sealed class PublicGetHotelHandler(PublicHotelReader hotels, PublicContentReader content)
    : IRequestHandler<PublicGetHotelRequest, PublicHotelResponse>
{
    public async Task<PublicHotelResponse> Handle(
        PublicGetHotelRequest request,
        CancellationToken cancellationToken)
    {
        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var hotel = context.Hotel;
        var culture = RequestCulture.Current;

        var images = await content.GetHotelImagesAsync(culture, cancellationToken).ConfigureAwait(false);
        var description = await content
            .GetHotelDescriptionAsync(context.HotelId, culture, cancellationToken)
            .ConfigureAwait(false);

        var settings = hotel.PublicBookingSettings;
        var policy = hotel.CancellationPolicy;
        var tax = hotel.TaxProfile;

        return new PublicHotelResponse
        {
            Slug = hotel.PublicSlug!,
            BrandName = context.BrandName,
            Name = hotel.Name,
            Description = description,
            AddressLine = hotel.AddressLine,
            PostalCode = hotel.PostalCode,
            City = hotel.City,
            Country = hotel.Country.ToString(),
            Phone = hotel.Phone,
            Email = hotel.Email,
            Currency = hotel.Currency,
            TimeZoneId = hotel.TimeZoneId,
            DefaultCulture = hotel.DefaultCulture,
            SupportedCultures = SupportedCultures.All,
            CheckInFromLocal = hotel.CheckInFromLocal,
            CheckOutUntilLocal = hotel.CheckOutUntilLocal,
            Images = images,
            Amenities = PublicContentReader.Amenities(hotel.Amenities),
            Booking = new PublicBookingSettingsResponse
            {
                MinNights = settings.MinNights,
                MaxNights = settings.MaxNights,
                MaxAdvanceDays = settings.MaxAdvanceDays,
                MinAdvanceHours = settings.MinAdvanceHours,
                MaxAdults = settings.MaxAdults,
                MaxChildren = settings.MaxChildren,
                ConfirmationMode = settings.ConfirmationMode.ToString()
            },
            CityTax = new PublicCityTaxInfoResponse
            {
                Applies = tax.CityTaxEnabled && tax.CityTaxPerPersonNight > 0m,
                PerPersonNight = tax.CityTaxPerPersonNight,
                Currency = hotel.Currency,
                ChildrenExempt = tax.CityTaxExemptChildren,
                ChildAgeLimit = tax.CityTaxChildAgeLimit,
                ChargedOnlyIfStayTakesPlace = true
            },
            CancellationPolicy = new PublicHotelCancellationPolicyResponse
            {
                Type = policy.Type.ToString(),
                FreeCancellationDaysBeforeArrival = policy.FreeCancellationDaysBeforeArrival,
                CutoffLocalTime = policy.CutoffLocalTime,
                LateCancellationFeePercent = policy.LateCancellationFeePercent,
                NoShowFeePercent = policy.NoShowFeePercent,
                AppliesToAccommodationOnly = true
            },
            PaymentOptions = PublicPaymentOptions.PayAtProperty
        };
    }
}
