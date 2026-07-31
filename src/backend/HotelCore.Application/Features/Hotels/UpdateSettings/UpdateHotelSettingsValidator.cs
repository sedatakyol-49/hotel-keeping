using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Hotels.UpdateSettings;

/// <summary>
/// api-contracts.md → "Hotels &amp; Ayarlar" doğrulama kuralları.
/// </summary>
public sealed class UpdateHotelSettingsValidator : AbstractValidator<UpdateHotelSettingsRequest>
{
    private const int MaxNameLength = 200;
    private const int MaxCityLength = 100;
    private const int MaxAddressLength = 200;
    private const int MaxPostalCodeLength = 20;
    private const int MaxPhoneLength = 50;
    private const int MaxEmailLength = 200;
    private const int MaxTaxNumberLength = 50;

    /// <summary>Kurtaxe çocuk muafiyeti yaş sınırı üst değeri (DB: <c>CK_Hotels_CityTaxChildAgeLimit</c>).</summary>
    private const int MaxChildAgeLimit = 99;

    // --- Misafire açık kanal (api-contracts-public-booking.md §10) ---------------------------
    private const int MaxVatIdLength = 32;
    private const int MaxTimeZoneLength = 64;
    private const int MaxHostLength = 253;
    private const int MaxLegalEntityNameLength = 200;
    private const int MaxShortTextLength = 100;
    private const int MaxMinNights = 30;
    private const int MaxMaxNights = 365;
    private const int MaxAdvanceDays = 730;
    private const int MaxAdvanceHours = 72;
    private const int MaxOccupancy = 20;
    private const int MaxFreeCancellationDays = 90;

    /// <summary>
    /// <c>Hotel.PublicSlug</c> biçimi: küçük harf, <c>a-z0-9-</c>, 3–60 karakter, baş/son tire
    /// yok. Desen sözleşmedekiyle <b>birebir</b> aynıdır.
    /// </summary>
    private const string SlugPattern = "^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$";

    public UpdateHotelSettingsValidator()
    {
        RuleFor(request => request.Id).NotEmpty();

        RuleFor(request => request.Name).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.City).NotEmpty().MaximumLength(MaxCityLength);
        RuleFor(request => request.Country).IsInEnum();

        RuleFor(request => request.AddressLine).MaximumLength(MaxAddressLength);
        RuleFor(request => request.PostalCode).MaximumLength(MaxPostalCodeLength);
        RuleFor(request => request.Phone).MaximumLength(MaxPhoneLength);
        RuleFor(request => request.TaxNumber).MaximumLength(MaxTaxNumberLength);

        RuleFor(request => request.Email)
            .MaximumLength(MaxEmailLength)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        // Dil kumesi Application katmaninda tutulur: validator HTTP yapilandirmasina
        // (Localization:SupportedCultures) bagimli olmamalidir.
        RuleFor(request => request.DefaultCulture)
            .NotEmpty()
            .Must(SupportedCultures.IsSupported)
            .WithMessage(_ => Messages.SupportedCultureList);

        // ISO 4217: tam 3 buyuk harf. Kod listesi dogrulanmaz (yeni para birimi eklenince
        // uygulamanin guncellenmesi gerekmesin diye), yalnizca bicim.
        RuleFor(request => request.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage(_ => Messages.CurrencyFormat);

        RuleFor(request => request.TaxProfile).NotNull();

        When(request => request.TaxProfile is not null, () =>
        {
            RuleFor(request => request.TaxProfile.VatRate).InclusiveBetween(0m, 100m);
            RuleFor(request => request.TaxProfile.ReducedVatRate).InclusiveBetween(0m, 100m);
            RuleFor(request => request.TaxProfile.CityTaxPerPersonNight).GreaterThanOrEqualTo(0m);

            // Yas siniri: null (bilinmiyor) ya da 0-99. Aralik veritabanindaki
            // CK_Hotels_CityTaxChildAgeLimit kisiti ile BIREBIR aynidir; burada olmasinin nedeni
            // kullaniciya 500 yerine anlamli 400 dondurmektir.
            RuleFor(request => request.TaxProfile.CityTaxChildAgeLimit)
                .InclusiveBetween(0, MaxChildAgeLimit)
                .When(request => request.TaxProfile.CityTaxChildAgeLimit is not null)
                .WithMessage(_ => Messages.ChildAgeLimit(MaxChildAgeLimit));

            // DIKKAT: cityTaxExemptChildren = true iken yas siniri ZORUNLU DEGILDIR. Muafiyetin
            // varligi hesabi belirler (cocuklar sayilmaz); sinir yalnizca belgelenen dayanaktir ve
            // otel bunu bilmeyebilir. Zorunlu kilmak muafiyeti acmayi gereksiz yere engellerdi.
        });

        AddPublicChannelRules();
    }

    /// <summary>
    /// Misafire açık kanal ayarlarının doğrulaması
    /// (api-contracts-public-booking.md §10). <b>Yeni izin anahtarı yoktur</b>: bu alanlar
    /// mevcut <c>Settings.Manage</c> izniyle yönetilir.
    /// </summary>
    private void AddPublicChannelRules()
    {
        RuleFor(request => request.VatId).MaximumLength(MaxVatIdLength);

        // Saat dilimi HER ZAMAN doğrulanır (kanal kapalıyken de): "otelin bugünü" kavramı
        // rezervasyon tarafında da kullanılır ve geçersiz bir kimlik sessizce UTC'ye düşerdi.
        RuleFor(request => request.TimeZoneId)
            .NotEmpty()
            .MaximumLength(MaxTimeZoneLength)
            .Must(BeAKnownTimeZone)
            .WithMessage(_ => Messages.InvalidTimeZone);

        RuleFor(request => request.PublicBooking).NotNull();
        RuleFor(request => request.CancellationPolicy).NotNull();
        RuleFor(request => request.LegalProfile).NotNull();

        When(request => request.PublicBooking is not null, () =>
        {
            RuleFor(request => request.PublicBooking.MinNights).InclusiveBetween(1, MaxMinNights);
            RuleFor(request => request.PublicBooking.MaxNights)
                .InclusiveBetween(1, MaxMaxNights)
                .GreaterThanOrEqualTo(request => request.PublicBooking.MinNights);
            RuleFor(request => request.PublicBooking.MaxAdvanceDays).InclusiveBetween(1, MaxAdvanceDays);
            RuleFor(request => request.PublicBooking.MinAdvanceHours).InclusiveBetween(0, MaxAdvanceHours);
            RuleFor(request => request.PublicBooking.MaxAdults).InclusiveBetween(1, MaxOccupancy);
            RuleFor(request => request.PublicBooking.MaxChildren).InclusiveBetween(0, MaxOccupancy);

            RuleFor(request => request.PublicBooking.ConfirmationMode)
                .Must(value => Enum.TryParse<PublicBookingConfirmationMode>(value, out _))
                .WithMessage(_ => Messages.InvalidConfirmationMode);

            RuleFor(request => request.PublicBooking.Slug)
                .Matches(SlugPattern)
                .WithMessage(_ => Messages.InvalidPublicSlug)
                .When(request => !string.IsNullOrWhiteSpace(request.PublicBooking.Slug));

            RuleFor(request => request.PublicBooking.Host).MaximumLength(MaxHostLength);

            // Kanal AÇILIRKEN slug ve künye zorunludur: slug'sız bir kanalın URL'i yoktur,
            // künyesiz bir kanal §5 DDG ihlalidir. Kapalı kanalda ikisi de boş kalabilir.
            When(request => request.PublicBooking.IsEnabled, () =>
            {
                RuleFor(request => request.PublicBooking.Slug)
                    .NotEmpty()
                    .WithMessage(_ => Messages.PublicSlugRequired);

                RuleFor(request => request.LegalProfile.LegalEntityName)
                    .NotEmpty()
                    .MaximumLength(MaxLegalEntityNameLength)
                    .WithMessage(_ => Messages.LegalEntityNameRequired);
            });
        });

        When(request => request.CancellationPolicy is not null, () =>
        {
            RuleFor(request => request.CancellationPolicy.Type)
                .Must(value => Enum.TryParse<CancellationPolicyType>(value, out _))
                .WithMessage(_ => Messages.InvalidCancellationPolicyType);

            RuleFor(request => request.CancellationPolicy.FreeCancellationDaysBeforeArrival)
                .InclusiveBetween(0, MaxFreeCancellationDays);

            RuleFor(request => request.CancellationPolicy.LateCancellationFeePercent)
                .InclusiveBetween(0m, 100m);

            RuleFor(request => request.CancellationPolicy.NoShowFeePercent)
                .InclusiveBetween(0m, 100m);
        });

        When(request => request.LegalProfile is not null, () =>
        {
            RuleFor(request => request.LegalProfile.LegalEntityName)
                .MaximumLength(MaxLegalEntityNameLength);
            RuleFor(request => request.LegalProfile.LegalForm).MaximumLength(MaxShortTextLength);
            RuleFor(request => request.LegalProfile.RepresentedBy).MaximumLength(MaxNameLength);
            RuleFor(request => request.LegalProfile.AddressLine).MaximumLength(MaxAddressLength);
            RuleFor(request => request.LegalProfile.PostalCode).MaximumLength(MaxPostalCodeLength);
            RuleFor(request => request.LegalProfile.City).MaximumLength(MaxCityLength);
            RuleFor(request => request.LegalProfile.Phone).MaximumLength(MaxPhoneLength);
            RuleFor(request => request.LegalProfile.Email).MaximumLength(MaxEmailLength);
            RuleFor(request => request.LegalProfile.RegisterCourt).MaximumLength(MaxNameLength);
            RuleFor(request => request.LegalProfile.RegisterNumber).MaximumLength(MaxShortTextLength);
            RuleFor(request => request.LegalProfile.SupervisoryAuthority).MaximumLength(MaxNameLength);

            RuleFor(request => request.LegalProfile.Country)
                .Must(value => Enum.TryParse<Country>(value, ignoreCase: true, out _))
                .When(request => !string.IsNullOrWhiteSpace(request.LegalProfile.Country))
                .WithMessage(_ => Messages.PublicCountryUnknown);
        });
    }

    /// <summary>
    /// IANA kimliği çözülebiliyor mu. <b>Neden doğrulanıyor:</b> geçersiz bir kimlik çalışma
    /// zamanında sessizce UTC'ye düşer ve iptal politikası saatlerce kayar — kullanıcı bunu
    /// ancak bir misafir şikâyet ettiğinde fark ederdi.
    /// </summary>
    private static bool BeAKnownTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
