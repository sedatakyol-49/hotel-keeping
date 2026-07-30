using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Balances;

internal sealed class ListVacationBalancesHandler(VacationReader reader, IDateTimeProvider clock)
    : IRequestHandler<ListVacationBalancesRequest, IReadOnlyList<VacationBalanceResponse>>
{
    public Task<IReadOnlyList<VacationBalanceResponse>> Handle(
        ListVacationBalancesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Varsayilan yil sunucu saatinden (UTC) gelir; istemci saatine guvenilmez.
        var year = request.Year ?? clock.UtcNow.UtcDateTime.Year;

        return reader.ListBalancesAsync(request.EmployeeId, year, cancellationToken);
    }
}
