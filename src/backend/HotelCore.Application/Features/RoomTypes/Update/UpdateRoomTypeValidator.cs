using FluentValidation;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.Update;

/// <summary>Ortak oda tipi kuralları + route'tan gelen kimliğin dolu olması.</summary>
public sealed class UpdateRoomTypeValidator : RoomTypeWriteValidator<UpdateRoomTypeRequest>
{
    public UpdateRoomTypeValidator() => RuleFor(request => request.Id).NotEmpty();
}
