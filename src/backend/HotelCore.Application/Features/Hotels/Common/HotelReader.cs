using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Hotels.Common;

/// <summary>
/// Otel okuma yolu — erişim kapsamı ve projeksiyonlar tek yerde.
/// <para>
/// <b>Neden elle filtre:</b> <see cref="Hotel"/> tenant-scoped bir entity değildir (tenant
/// kökünün kendisidir), bu yüzden <c>AppDbContext</c>'teki global query filter onu süzmez.
/// Erişim <see cref="UserHotelAccess"/> tablosundan doğrulanır — JWT claim'i yerine veritabanı
/// esas alınır, böylece erişim iptali token süresinin bitmesini beklemez.
/// </para>
/// </summary>
internal sealed class HotelReader(IAppDbContext database, ICurrentUser currentUser)
{
    /// <summary>
    /// Kullanıcının erişebildiği oteller. <c>allHotels</c> yetkisi varsa kullanıcının bağlı
    /// olduğu Head Office'in tüm otelleri; yoksa yalnızca <see cref="UserHotelAccess"/> ile
    /// açıkça verilmiş oteller.
    /// </summary>
    public IQueryable<Hotel> AccessibleHotels()
    {
        var hotels = database.Hotels.AsQueryable();

        if (currentUser.CanAccessAllHotels)
        {
            // Head Office kapsamı yine sınırlıdır: başka markanın otelleri görünmez.
            return currentUser.HeadOfficeId is { } headOfficeId
                ? hotels.Where(hotel => hotel.HeadOfficeId == headOfficeId)
                : hotels.Where(_ => false);
        }

        var userId = currentUser.UserId;

        return userId is null
            ? hotels.Where(_ => false)
            : hotels.Where(hotel =>
                database.UserHotelAccesses.Any(access =>
                    access.UserId == userId && access.HotelId == hotel.Id));
    }

    public async Task<IReadOnlyList<HotelListItemResponse>> ListAsync(
        CancellationToken cancellationToken) =>
        await AccessibleHotels()
            .OrderBy(hotel => hotel.Name)
            .Select(hotel => new HotelListItemResponse
            {
                Id = hotel.Id,
                Name = hotel.Name,
                City = hotel.City,
                Country = hotel.Country.ToString(),
                Currency = hotel.Currency,
                DefaultCulture = hotel.DefaultCulture,
                RoomCount = hotel.Rooms.Count(room => !room.IsDeleted),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Tek otel. Erişilemeyen otel <b>404</b> döner (403 değil): otelin var olduğu bilgisi
    /// sızdırılmaz — oda modülündeki tenant izolasyonu davranışıyla aynı.
    /// </summary>
    public async Task<HotelResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var hotel = await AccessibleHotels()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new HotelResponse
            {
                VatId = candidate.VatId,
                TimeZoneId = candidate.TimeZoneId,
                CheckInFromLocal = candidate.CheckInFromLocal,
                CheckOutUntilLocal = candidate.CheckOutUntilLocal,
                PublicBooking = new PublicBookingSettingsDto
                {
                    IsEnabled = candidate.PublicBookingSettings.IsEnabled,
                    Slug = candidate.PublicSlug,
                    Host = candidate.PublicHost,
                    MinNights = candidate.PublicBookingSettings.MinNights,
                    MaxNights = candidate.PublicBookingSettings.MaxNights,
                    MaxAdvanceDays = candidate.PublicBookingSettings.MaxAdvanceDays,
                    MinAdvanceHours = candidate.PublicBookingSettings.MinAdvanceHours,
                    MaxAdults = candidate.PublicBookingSettings.MaxAdults,
                    MaxChildren = candidate.PublicBookingSettings.MaxChildren,
                    ConfirmationMode = candidate.PublicBookingSettings.ConfirmationMode.ToString(),
                },
                CancellationPolicy = new CancellationPolicyDto
                {
                    Type = candidate.CancellationPolicy.Type.ToString(),
                    FreeCancellationDaysBeforeArrival =
                        candidate.CancellationPolicy.FreeCancellationDaysBeforeArrival,
                    CutoffLocalTime = candidate.CancellationPolicy.CutoffLocalTime,
                    LateCancellationFeePercent = candidate.CancellationPolicy.LateCancellationFeePercent,
                    NoShowFeePercent = candidate.CancellationPolicy.NoShowFeePercent,
                },
                LegalProfile = new HotelLegalProfileDto
                {
                    LegalEntityName = candidate.LegalProfile.LegalEntityName,
                    LegalForm = candidate.LegalProfile.LegalForm,
                    RepresentedBy = candidate.LegalProfile.RepresentedBy,
                    AddressLine = candidate.LegalProfile.AddressLine,
                    PostalCode = candidate.LegalProfile.PostalCode,
                    City = candidate.LegalProfile.City,
                    Country = candidate.LegalProfile.Country == null
                        ? null
                        : candidate.LegalProfile.Country.ToString(),
                    Phone = candidate.LegalProfile.Phone,
                    Email = candidate.LegalProfile.Email,
                    RegisterCourt = candidate.LegalProfile.RegisterCourt,
                    RegisterNumber = candidate.LegalProfile.RegisterNumber,
                    SupervisoryAuthority = candidate.LegalProfile.SupervisoryAuthority,
                    ParticipatesInDisputeResolution =
                        candidate.LegalProfile.ParticipatesInDisputeResolution,
                    OnlineDisputeResolutionUrl = candidate.LegalProfile.OnlineDisputeResolutionUrl,
                    DisputeResolutionNotice = candidate.LegalProfile.DisputeResolutionNotice,
                },
                Id = candidate.Id,
                HeadOfficeId = candidate.HeadOfficeId,
                Name = candidate.Name,
                Country = candidate.Country.ToString(),
                City = candidate.City,
                AddressLine = candidate.AddressLine,
                PostalCode = candidate.PostalCode,
                Phone = candidate.Phone,
                Email = candidate.Email,
                TaxNumber = candidate.TaxNumber,
                DefaultCulture = candidate.DefaultCulture,
                Currency = candidate.Currency,
                RoomCount = candidate.Rooms.Count(room => !room.IsDeleted),
                TaxProfile = new TaxProfileDto
                {
                    VatRate = candidate.TaxProfile.VatRate,
                    ReducedVatRate = candidate.TaxProfile.ReducedVatRate,
                    CityTaxPerPersonNight = candidate.TaxProfile.CityTaxPerPersonNight,
                    CityTaxEnabled = candidate.TaxProfile.CityTaxEnabled,
                    CityTaxExemptChildren = candidate.TaxProfile.CityTaxExemptChildren,
                    CityTaxChildAgeLimit = candidate.TaxProfile.CityTaxChildAgeLimit,
                },
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (hotel is null)
        {
            throw new NotFoundException(Messages.HotelNotFound);
        }

        // Donanım listesi kolonda CSV'dir; dönüşüm kuralı tek yerdedir (AmenityList) ve EF
        // izdüşümünde çalıştırılamaz, bu yüzden materyalizasyondan sonra uygulanır.
        var amenities = AmenityList.Parse(
            await AccessibleHotels()
                .Where(candidate => candidate.Id == id)
                .Select(candidate => candidate.Amenities)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false));

        return hotel with
        {
            Amenities = amenities,
            Warnings = await BuildWarningsAsync(id, hotel, cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Engelleyici olmayan yapılandırma uyarıları.
    /// <para>
    /// <b><c>NoRatePlanForWebsiteChannel</c>:</b> fiyat seçimi kanalı <b>birebir</b> karşılaştırır
    /// (<c>ReservationPricingService</c>), yani <c>Channel = Direct</c> planları web
    /// rezervasyonlarına uygulanmaz. Otelin <c>Website</c> ya da "tüm kanallar"
    /// (<c>Channel = null</c>) planı yoksa web fiyatı sessizce <c>RoomType.BasePrice</c>'a düşer —
    /// bu, kanal açılırken görülmesi gereken bir sürprizdir
    /// (architecture-public-booking.md §7.1).
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<string>> BuildWarningsAsync(
        Guid hotelId,
        HotelResponse hotel,
        CancellationToken cancellationToken)
    {
        if (!hotel.PublicBooking.IsEnabled)
        {
            return [];
        }

        var hasApplicablePlan = await database.RatePlans
            .AnyAsync(
                plan => plan.HotelId == hotelId
                        && plan.IsActive
                        && (plan.Channel == null || plan.Channel == ReservationChannel.Website),
                cancellationToken)
            .ConfigureAwait(false);

        return hasApplicablePlan ? [] : ["NoRatePlanForWebsiteChannel"];
    }

    /// <summary>Yazma yolu için izlenen (tracked) varlık; erişilemiyorsa 404.</summary>
    public async Task<Hotel> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var hotel = await AccessibleHotels()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return hotel ?? throw new NotFoundException(Messages.HotelNotFound);
    }
}
