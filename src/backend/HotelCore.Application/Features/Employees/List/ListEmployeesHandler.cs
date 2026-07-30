using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Employees.Common;

namespace HotelCore.Application.Features.Employees.List;

internal sealed class ListEmployeesHandler(EmployeeReader reader)
    : IRequestHandler<ListEmployeesRequest, PagedResult<EmployeeResponse>>
{
    public Task<PagedResult<EmployeeResponse>> Handle(
        ListEmployeesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.ToQuery(), cancellationToken);
    }
}
