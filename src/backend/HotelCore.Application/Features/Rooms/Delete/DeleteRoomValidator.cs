using FluentValidation;

namespace HotelCore.Application.Features.Rooms.Delete;

/// <summary>Biçimsel kontrol; rezervasyon kuralı handler'da denetlenir.</summary>
public sealed class DeleteRoomValidator : AbstractValidator<DeleteRoomRequest>
{
    public DeleteRoomValidator() => RuleFor(request => request.Id).NotEmpty();
}
