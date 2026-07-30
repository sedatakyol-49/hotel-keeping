using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Create;

public sealed class CreateReservationValidator : ReservationWriteValidator<CreateReservationRequest>
{
    public CreateReservationValidator() =>
        RuleFor(request => request.Status)
            .Must(status => status is ReservationStatus.Option or ReservationStatus.Confirmed)
            .WithMessage(_ => Messages.InitialReservationStatus)
            .When(request => request.Status is not null);
}
