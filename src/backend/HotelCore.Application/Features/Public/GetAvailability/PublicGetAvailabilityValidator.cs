using FluentValidation;

namespace HotelCore.Application.Features.Public.GetAvailability;

/// <summary>
/// Yalnızca <b>biçim</b> kuralları. Otele bağlı eşikler (min/max gece, maxAdvanceDays,
/// maxAdults …) <c>PublicStayRules</c> içindedir: onların kaynağı veritabanıdır ve validator
/// veritabanına gitmez.
/// </summary>
public sealed class PublicGetAvailabilityValidator : AbstractValidator<PublicGetAvailabilityRequest>
{
    public PublicGetAvailabilityValidator()
    {
        RuleFor(request => request.CheckIn).NotEmpty();
        RuleFor(request => request.CheckOut).NotEmpty();
        RuleFor(request => request.Adults).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Children).GreaterThanOrEqualTo(0);
    }
}
