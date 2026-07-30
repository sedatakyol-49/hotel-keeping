using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Employees.Common;

namespace HotelCore.Application.Features.Employees.GetById;

internal sealed class GetEmployeeByIdHandler(EmployeeReader reader)
    : IRequestHandler<GetEmployeeByIdRequest, EmployeeResponse>
{
    public Task<EmployeeResponse> Handle(
        GetEmployeeByIdRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
