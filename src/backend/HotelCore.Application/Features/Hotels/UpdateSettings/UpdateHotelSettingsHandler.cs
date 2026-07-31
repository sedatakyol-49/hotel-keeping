using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Hotels.UpdateSettings;

/// <summary>
/// Otel künyesini ve vergi profilini günceller. Erişilemeyen otel 404 döner
/// (bkz. <see cref="HotelReader"/>).
/// </summary>
internal sealed class UpdateHotelSettingsHandler(
    IAppDbContext database,
    HotelReader reader)
    : IRequestHandler<UpdateHotelSettingsRequest, HotelResponse>
{
    public async Task<HotelResponse> Handle(
        UpdateHotelSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotel = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        hotel.Name = request.Name.Trim();
        hotel.Country = request.Country;
        hotel.City = request.City.Trim();
        hotel.AddressLine = Normalize(request.AddressLine);
        hotel.PostalCode = Normalize(request.PostalCode);
        hotel.Phone = Normalize(request.Phone);
        hotel.Email = Normalize(request.Email);
        hotel.TaxNumber = Normalize(request.TaxNumber);
        hotel.DefaultCulture = SupportedCultures.Normalize(request.DefaultCulture);
        hotel.Currency = request.Currency.Trim().ToUpperInvariant();

        hotel.TaxProfile.VatRate = request.TaxProfile.VatRate;
        hotel.TaxProfile.ReducedVatRate = request.TaxProfile.ReducedVatRate;
        hotel.TaxProfile.CityTaxPerPersonNight = request.TaxProfile.CityTaxPerPersonNight;
        hotel.TaxProfile.CityTaxEnabled = request.TaxProfile.CityTaxEnabled;

        // Kurtaxe cocuk muafiyeti. Yas siniri muafiyet KAPALIYKEN de saklanir: otelin belediye
        // kurali (orn. "18 yas alti") muafiyet gecici olarak kapatildiginda kaybolmasin.
        hotel.TaxProfile.CityTaxExemptChildren = request.TaxProfile.CityTaxExemptChildren;
        hotel.TaxProfile.CityTaxChildAgeLimit = request.TaxProfile.CityTaxChildAgeLimit;

        await ApplyPublicChannelAsync(hotel, request, cancellationToken).ConfigureAwait(false);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Misafire açık kanal ayarlarını uygular.
    ///
    /// <para><b>Slug global benzersizdir</b> (marka bazında değil): URL uzayı globaldir. Ön
    /// kontrol kullanıcıya anlamlı bir <b>409</b> vermek içindir; nihai güvence
    /// <c>Hotels.PublicSlug</c> üzerindeki kısmi unique index'tir ve yarış durumunda
    /// <c>AppDbContext</c> onu da 409'a çevirir.</para>
    ///
    /// <para><b>Kanal kapatıldığında slug SİLİNMEZ:</b> kanal geri açıldığında aynı URL geri
    /// gelmelidir — arama motorlarındaki ve misafirin yer imlerindeki bağlantılar kalıcıdır.
    /// Kapalı kanalda uçlar zaten 404 döner (<c>IsEnabled = false</c>).</para>
    /// </summary>
    private async Task ApplyPublicChannelAsync(
        Hotel hotel,
        UpdateHotelSettingsRequest request,
        CancellationToken cancellationToken)
    {
        hotel.VatId = Normalize(request.VatId);
        hotel.TimeZoneId = request.TimeZoneId.Trim();
        hotel.CheckInFromLocal = request.CheckInFromLocal;
        hotel.CheckOutUntilLocal = request.CheckOutUntilLocal;
        hotel.Amenities = AmenityList.Format(request.Amenities);

        var slug = Normalize(request.PublicBooking.Slug)?.ToLowerInvariant();
        if (slug is not null && !string.Equals(hotel.PublicSlug, slug, StringComparison.Ordinal))
        {
            var taken = await database.Hotels
                .AnyAsync(other => other.Id != hotel.Id && other.PublicSlug == slug, cancellationToken)
                .ConfigureAwait(false);

            if (taken)
            {
                throw new ConflictException(Messages.PublicSlugTaken(slug));
            }
        }

        hotel.PublicSlug = slug ?? hotel.PublicSlug;
        hotel.PublicHost = Normalize(request.PublicBooking.Host);

        hotel.PublicBookingSettings = new PublicBookingSettings
        {
            IsEnabled = request.PublicBooking.IsEnabled,
            MinNights = request.PublicBooking.MinNights,
            MaxNights = request.PublicBooking.MaxNights,
            MaxAdvanceDays = request.PublicBooking.MaxAdvanceDays,
            MinAdvanceHours = request.PublicBooking.MinAdvanceHours,
            MaxAdults = request.PublicBooking.MaxAdults,
            MaxChildren = request.PublicBooking.MaxChildren,
            ConfirmationMode = ParseEnum(
                request.PublicBooking.ConfirmationMode,
                PublicBookingConfirmationMode.Instant)
        };

        hotel.CancellationPolicy = new CancellationPolicy
        {
            Type = ParseEnum(request.CancellationPolicy.Type, CancellationPolicyType.Flexible),
            FreeCancellationDaysBeforeArrival =
                request.CancellationPolicy.FreeCancellationDaysBeforeArrival,
            CutoffLocalTime = request.CancellationPolicy.CutoffLocalTime,
            LateCancellationFeePercent = request.CancellationPolicy.LateCancellationFeePercent,
            NoShowFeePercent = request.CancellationPolicy.NoShowFeePercent
        };

        hotel.LegalProfile = new HotelLegalProfile
        {
            LegalEntityName = Normalize(request.LegalProfile.LegalEntityName),
            LegalForm = Normalize(request.LegalProfile.LegalForm),
            RepresentedBy = Normalize(request.LegalProfile.RepresentedBy),
            AddressLine = Normalize(request.LegalProfile.AddressLine),
            PostalCode = Normalize(request.LegalProfile.PostalCode),
            City = Normalize(request.LegalProfile.City),
            Country = Enum.TryParse<Country>(request.LegalProfile.Country, ignoreCase: true, out var country)
                ? country
                : null,
            Phone = Normalize(request.LegalProfile.Phone),
            Email = Normalize(request.LegalProfile.Email),
            RegisterCourt = Normalize(request.LegalProfile.RegisterCourt),
            RegisterNumber = Normalize(request.LegalProfile.RegisterNumber),
            SupervisoryAuthority = Normalize(request.LegalProfile.SupervisoryAuthority),
            ParticipatesInDisputeResolution = request.LegalProfile.ParticipatesInDisputeResolution,
            OnlineDisputeResolutionUrl = Normalize(request.LegalProfile.OnlineDisputeResolutionUrl),
            DisputeResolutionNotice = Normalize(request.LegalProfile.DisputeResolutionNotice)
        };
    }

    /// <summary>Validator geçerliliği zaten doğruladı; burada yalnızca güvenli bir çözüm yapılır.</summary>
    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    /// <summary>Bos/bosluk metni <c>null</c>'a indirger — "" ile null ayrimi veride tutulmaz.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
