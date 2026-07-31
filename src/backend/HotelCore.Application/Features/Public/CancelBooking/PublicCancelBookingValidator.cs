using FluentValidation;

namespace HotelCore.Application.Features.Public.CancelBooking;

/// <summary>Biçim kuralları; ücret mutabakatı handler'da (sunucu hesabıyla) yapılır.</summary>
public sealed class PublicCancelBookingValidator : AbstractValidator<PublicCancelBookingRequest>
{
    private const int MaxReasonLength = 500;

    public PublicCancelBookingValidator()
    {
        RuleFor(request => request.AccessToken).NotEmpty();
        RuleFor(request => request.Reason).MaximumLength(MaxReasonLength);
        RuleFor(request => request.AcknowledgedFeeAmount)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.AcknowledgedFeeAmount is not null);
    }
}
