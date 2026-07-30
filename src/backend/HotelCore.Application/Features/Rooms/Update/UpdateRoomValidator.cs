using FluentValidation;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.Update;

/// <summary>Ortak oda kuralları + kimlik ve geçerli enum değeri.</summary>
public sealed class UpdateRoomValidator : RoomWriteValidator<UpdateRoomRequest>
{
    public UpdateRoomValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.HousekeepingStatus).IsInEnum();
    }
}
