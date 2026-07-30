using FluentValidation;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Oda tipi yazma isteklerinin ortak doğrulaması (api-contracts.md → "Doğrulama kuralları"):
/// <c>code</c> 1–10 karakter, <c>basePrice</c> ≥ 0, <c>capacity</c> 1–20,
/// <c>sizeSqm</c> &gt; 0 veya null. Kodun otel içindeki <b>benzersizliği</b> burada değil
/// handler'da kontrol edilir (çakışma 409, doğrulama hatası değil).
/// </summary>
/// <typeparam name="TRequest">Create veya Update isteği.</typeparam>
public abstract class RoomTypeWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IRoomTypeWriteRequest
{
    /// <summary>Sözleşmedeki kod uzunluğu sınırı (DB kolonu 16 karaktere kadar izin verir).</summary>
    private const int MaxCodeLength = 10;

    private const int MaxNameLength = 150;

    private const int MaxDescriptionLength = 1000;

    protected RoomTypeWriteValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(MaxCodeLength);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(request => request.Description)
            .MaximumLength(MaxDescriptionLength);

        RuleFor(request => request.BasePrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.Capacity)
            .InclusiveBetween(1, 20);

        RuleFor(request => request.SizeSqm)
            .GreaterThan(0)
            .When(request => request.SizeSqm.HasValue);

        RuleFor(request => request.Amenities)
            .Must(amenities => amenities is null || amenities.Count <= AmenityList.MaxItemCount)
            .WithMessage($"En fazla {AmenityList.MaxItemCount} donanim anahtari gonderilebilir.")
            .Must(amenities => amenities is null
                               || amenities.All(item => item.Trim().Length <= AmenityList.MaxItemLength))
            .WithMessage($"Her donanim anahtari en fazla {AmenityList.MaxItemLength} karakter olabilir.")
            .Must(amenities => (AmenityList.Format(amenities)?.Length ?? 0) <= AmenityList.MaxStoredLength)
            .WithMessage($"Donanim listesi toplam {AmenityList.MaxStoredLength} karakteri gecemez.");

        RuleFor(request => request.Translations)
            .Must(translations => translations is null
                                  || translations.Keys.All(SupportedCultures.IsSupported))
            .WithMessage($"Desteklenen dil kodlari: {string.Join(", ", SupportedCultures.All)}.")
            .Must(translations => translations is null
                                  || translations.Values.All(value =>
                                      value?.Name is null || value.Name.Trim().Length <= MaxNameLength))
            .WithMessage($"Ceviri adi en fazla {MaxNameLength} karakter olabilir.")
            .Must(translations => translations is null
                                  || translations.Values.All(value =>
                                      value?.Description is null
                                      || value.Description.Trim().Length <= MaxDescriptionLength))
            .WithMessage($"Ceviri aciklamasi en fazla {MaxDescriptionLength} karakter olabilir.");
    }
}
