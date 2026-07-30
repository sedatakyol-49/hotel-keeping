using FluentValidation;

namespace HotelCore.Application.Features.Rooms.GetById;

/// <summary>Boş GUID ile gereksiz sorgu yapılmaması için biçimsel kontrol.</summary>
public sealed class GetRoomByIdValidator : AbstractValidator<GetRoomByIdRequest>
{
    public GetRoomByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
