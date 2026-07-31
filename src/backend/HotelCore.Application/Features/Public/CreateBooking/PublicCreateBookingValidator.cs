using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.Public.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Public.CreateBooking;

/// <summary>
/// api-contracts-public-booking.md §6.2 doğrulama kuralları. Hata anahtarları <b>PascalCase</b>
/// ve iç içe alanlarda noktalıdır (<c>Guest.Email</c>) — FluentValidation'ın ürettiği yol.
/// </summary>
public sealed class PublicCreateBookingValidator : AbstractValidator<PublicCreateBookingRequest>
{
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 256;
    private const int MaxPhoneLength = 32;
    private const int MaxCompanyLength = 200;
    private const int MaxAddressLength = 256;
    private const int MaxPostalCodeLength = 16;
    private const int MaxCityLength = 100;
    private const int MaxVatIdLength = 32;
    private const int MaxGuestNoteLength = 500;
    private const int MinOrderButtonLabelLength = 1;
    private const int MaxOrderButtonLabelLength = 120;

    public PublicCreateBookingValidator()
    {
        RuleFor(request => request.HoldToken)
            .NotEmpty()
            .Must(token => PublicTokens.IsWellFormedUrlToken(token, PublicTokens.HoldTokenLength))
            .WithMessage(_ => Messages.PublicHoldTokenFormat);

        RuleFor(request => request.Checkout).NotNull();
        RuleFor(request => request.Guest).NotNull();
        RuleFor(request => request.Consents).NotNull();

        When(request => request.Checkout is not null, () =>
        {
            RuleFor(request => request.Checkout.SummaryHash)
                .NotEmpty()
                .Matches("^sha256:[0-9a-f]{64}$")
                .WithMessage(_ => Messages.PublicSummaryHashFormat);

            // İçerik DOĞRULANMAZ, yalnızca uzunluk: metin bir kanıt kaydıdır, bir kural değil.
            RuleFor(request => request.Checkout.OrderButtonLabel)
                .NotEmpty()
                .Length(MinOrderButtonLabelLength, MaxOrderButtonLabelLength);
        });

        When(request => request.Guest is not null, () =>
        {
            RuleFor(request => request.Guest.FirstName).NotEmpty().MaximumLength(MaxNameLength);
            RuleFor(request => request.Guest.LastName).NotEmpty().MaximumLength(MaxNameLength);

            RuleFor(request => request.Guest.Email)
                .NotEmpty()
                .MaximumLength(MaxEmailLength)
                .EmailAddress();

            RuleFor(request => request.Guest.Phone).MaximumLength(MaxPhoneLength);

            RuleFor(request => request.Guest.Culture)
                .NotEmpty()
                .Must(SupportedCultures.IsSupported)
                .WithMessage(_ => Messages.SupportedCultureList);

            RuleFor(request => request.Guest.CountryOfResidence)
                .Must(value => Enum.TryParse<Country>(value, ignoreCase: true, out _))
                .When(request => !string.IsNullOrWhiteSpace(request.Guest.CountryOfResidence))
                .WithMessage(_ => Messages.PublicCountryUnknown);
        });

        When(request => request.InvoiceAddress is not null, () =>
        {
            // Blok verildiyse adres satırı zorunludur: şirket adı tek başına bir fatura künyesi
            // değildir (§14 UStG alıcı adresi ister).
            RuleFor(request => request.InvoiceAddress!.AddressLine)
                .NotEmpty()
                .MaximumLength(MaxAddressLength);

            RuleFor(request => request.InvoiceAddress!.Company).MaximumLength(MaxCompanyLength);
            RuleFor(request => request.InvoiceAddress!.PostalCode).MaximumLength(MaxPostalCodeLength);
            RuleFor(request => request.InvoiceAddress!.City).MaximumLength(MaxCityLength);
            RuleFor(request => request.InvoiceAddress!.VatId).MaximumLength(MaxVatIdLength);

            RuleFor(request => request.InvoiceAddress!.Country)
                .Must(value => Enum.TryParse<Country>(value, ignoreCase: true, out _))
                .When(request => !string.IsNullOrWhiteSpace(request.InvoiceAddress!.Country))
                .WithMessage(_ => Messages.PublicCountryUnknown);
        });

        When(request => request.Stay is not null, () =>
            RuleFor(request => request.Stay.GuestNote).MaximumLength(MaxGuestNoteLength));

        When(request => request.Consents is not null, () =>
        {
            // Dört rıza da TRUE olmalıdır: onaysız rezervasyon sözleşme kurmaz.
            RuleFor(request => request.Consents.TermsAccepted)
                .Equal(true).WithMessage(_ => Messages.PublicConsentRequired);
            RuleFor(request => request.Consents.PrivacyNoticeAcknowledged)
                .Equal(true).WithMessage(_ => Messages.PublicConsentRequired);
            RuleFor(request => request.Consents.WithdrawalNoticeAcknowledged)
                .Equal(true).WithMessage(_ => Messages.PublicConsentRequired);
            RuleFor(request => request.Consents.BookerIsAdult)
                .Equal(true).WithMessage(_ => Messages.PublicConsentRequired);

            // marketingOptIn BİLİNÇLİ olarak zorunlu değildir ve varsayılanı false'tur:
            // ön işaretli bir onay kutusu DSGVO Art. 4 Nr. 11 anlamında geçerli rıza değildir.
        });
    }
}
