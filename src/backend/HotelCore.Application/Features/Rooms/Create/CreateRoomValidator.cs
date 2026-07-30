using FluentValidation;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.Create;

/// <summary>
/// Ortak oda kuralları + opsiyonel durumun geçerli enum değeri olması.
/// (Tanımsız bir metin gönderilirse model binding zaten 400 üretir; bu kural
/// <c>housekeepingStatus=99</c> gibi sayısal kaçakları da kapatır.)
/// </summary>
public sealed class CreateRoomValidator : RoomWriteValidator<CreateRoomRequest>
{
    public CreateRoomValidator() =>
        RuleFor(request => request.HousekeepingStatus)
            .IsInEnum()
            .When(request => request.HousekeepingStatus.HasValue);
}
