using FluentValidation;
using HotelCore.Application.Common.Localization;

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
            .WithMessage($"Desteklenen diller: {string.Join(", ", SupportedCultures.All)}.");

        // ISO 4217: tam 3 buyuk harf. Kod listesi dogrulanmaz (yeni para birimi eklenince
        // uygulamanin guncellenmesi gerekmesin diye), yalnizca bicim.
        RuleFor(request => request.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Para birimi ISO 4217 bicimi olmalidir (3 harf).");

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
                .WithMessage($"Yas siniri 0 ile {MaxChildAgeLimit} arasinda olmalidir.");

            // DIKKAT: cityTaxExemptChildren = true iken yas siniri ZORUNLU DEGILDIR. Muafiyetin
            // varligi hesabi belirler (cocuklar sayilmaz); sinir yalnizca belgelenen dayanaktir ve
            // otel bunu bilmeyebilir. Zorunlu kilmak muafiyeti acmayi gereksiz yere engellerdi.
        });
    }
}
