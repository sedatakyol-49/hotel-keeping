using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Employees.List;

/// <summary>
/// <c>GET /api/v1/employees</c> — sayfalı + filtreli çalışan listesi.
/// </summary>
public sealed record ListEmployeesRequest : IRequest<PagedResult<EmployeeResponse>>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    public Guid? DepartmentId { get; init; }

    public EmploymentType? EmploymentType { get; init; }

    /// <summary>Ad, soyad ve personel numarasında "contains" arama.</summary>
    public string? Search { get; init; }

    /// <summary>Varsayılan <c>false</c>: işten ayrılmışlar listelenmez.</summary>
    public bool IncludeTerminated { get; init; }

    internal EmployeeListQuery ToQuery() =>
        new(
            new PageQuery { Page = Page, PageSize = PageSize },
            DepartmentId,
            EmploymentType,
            Search,
            IncludeTerminated);
}
