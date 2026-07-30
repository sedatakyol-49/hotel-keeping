using FluentValidation;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Features.Guests.Common;

/// <summary>
/// Misafir yazma kuralları tek yerde — api-contracts-reservations.md → "Guests".
/// Uzunluk sınırları veritabanı kolonlarıyla (GuestConfiguration) uyumludur ki doğrulama
/// hatası 500 değil 400 olarak dönsün.
/// </summary>
/// <typeparam name="TRequest">Create veya Update isteği.</typeparam>
public abstract class GuestWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IGuestWriteRequest
{
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 256;
    private const int MaxPhoneLength = 32;
    private const int MaxAddressLength = 256;
    private const int MaxPostalCodeLength = 16;
    private const int MaxCityLength = 100;
    private const int MaxNoteLength = 1000;

    protected GuestWriteValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.Phone).MaximumLength(MaxPhoneLength);
        RuleFor(request => request.AddressLine).MaximumLength(MaxAddressLength);
        RuleFor(request => request.PostalCode).MaximumLength(MaxPostalCodeLength);
        RuleFor(request => request.City).MaximumLength(MaxCityLength);
        RuleFor(request => request.Note).MaximumLength(MaxNoteLength);

        RuleFor(request => request.Email)
            .MaximumLength(MaxEmailLength)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        RuleFor(request => request.Nationality)
            .IsInEnum()
            .When(request => request.Nationality is not null);

        // Yazışma dili yalnızca desteklenen diller olabilir (architecture.md §8).
        RuleFor(request => request.Culture)
            .Must(SupportedCultures.IsSupported)
            .WithMessage($"Desteklenen diller: {string.Join(", ", SupportedCultures.All)}.")
            .When(request => !string.IsNullOrWhiteSpace(request.Culture));

        // Dogum tarihi gelecekte olamaz; kontrol istekten bagimsiz oldugu icin sabit tarih yerine
        // "bugun"e gore yapilir (IDateTimeProvider validator'a enjekte edilmez: kural gun
        // hassasiyetinde ve UTC'ye gore yeterlidir).
        RuleFor(request => request.BirthDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .When(request => request.BirthDate is not null);
    }
}
