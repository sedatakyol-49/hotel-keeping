using FluentValidation;

namespace HotelCore.Application.Features.TimeEntries.Delete;

public sealed class DeleteTimeEntryValidator : AbstractValidator<DeleteTimeEntryRequest>
{
    public DeleteTimeEntryValidator() => RuleFor(request => request.Id).NotEmpty();
}
