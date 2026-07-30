using FluentValidation;

namespace HotelCore.Application.Features.Hotels.GetById;

public sealed class GetHotelByIdValidator : AbstractValidator<GetHotelByIdRequest>
{
    public GetHotelByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
