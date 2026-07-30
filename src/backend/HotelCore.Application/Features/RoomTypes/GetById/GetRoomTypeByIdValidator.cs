using FluentValidation;

namespace HotelCore.Application.Features.RoomTypes.GetById;

/// <summary>Boş GUID ile gereksiz veritabanı sorgusu yapılmaması için biçimsel kontrol.</summary>
public sealed class GetRoomTypeByIdValidator : AbstractValidator<GetRoomTypeByIdRequest>
{
    public GetRoomTypeByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
