using FluentValidation;

namespace HotelCore.Application.Features.RoomTypes.Delete;

/// <summary>Biçimsel kontrol; iş kuralı (bağlı oda) handler'da denetlenir.</summary>
public sealed class DeleteRoomTypeValidator : AbstractValidator<DeleteRoomTypeRequest>
{
    public DeleteRoomTypeValidator() => RuleFor(request => request.Id).NotEmpty();
}
