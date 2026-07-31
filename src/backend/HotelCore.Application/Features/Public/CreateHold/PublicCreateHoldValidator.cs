using FluentValidation;

namespace HotelCore.Application.Features.Public.CreateHold;

/// <summary>Biçim kuralları; otele bağlı eşikler <c>PublicStayRules</c> içindedir.</summary>
public sealed class PublicCreateHoldValidator : AbstractValidator<PublicCreateHoldRequest>
{
    /// <summary><c>RoomType.Code</c> kolon sınırı.</summary>
    private const int MaxRoomTypeCodeLength = 10;

    public PublicCreateHoldValidator()
    {
        RuleFor(request => request.RoomTypeCode).NotEmpty().MaximumLength(MaxRoomTypeCodeLength);
        RuleFor(request => request.CheckIn).NotEmpty();
        RuleFor(request => request.CheckOut).NotEmpty();
        RuleFor(request => request.Adults).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Children).GreaterThanOrEqualTo(0);
    }
}
