using FluentValidation;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Features.HeadOffices.UpdateSettings;

public sealed class UpdateHeadOfficeSettingsValidator
    : AbstractValidator<UpdateHeadOfficeSettingsRequest>
{
    private const int MaxBrandNameLength = 200;

    public UpdateHeadOfficeSettingsValidator()
    {
        RuleFor(request => request.BrandName).NotEmpty().MaximumLength(MaxBrandNameLength);

        RuleFor(request => request.DefaultCulture)
            .NotEmpty()
            .Must(SupportedCultures.IsSupported)
            .WithMessage($"Desteklenen diller: {string.Join(", ", SupportedCultures.All)}.");
    }
}
