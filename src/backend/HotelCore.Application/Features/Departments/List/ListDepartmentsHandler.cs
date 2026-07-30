using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;

namespace HotelCore.Application.Features.Departments.List;

internal sealed class ListDepartmentsHandler(DepartmentReader reader)
    : IRequestHandler<ListDepartmentsRequest, IReadOnlyList<DepartmentResponse>>
{
    public Task<IReadOnlyList<DepartmentResponse>> Handle(
        ListDepartmentsRequest request,
        CancellationToken cancellationToken) => reader.ListAsync(cancellationToken);
}
