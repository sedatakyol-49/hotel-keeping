using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.List;

/// <summary>
/// <c>GET /api/v1/time-entries</c> — sayfalı + filtreli zaman kaydı listesi
/// (en yeni mesai en üstte).
/// </summary>
public sealed record ListTimeEntriesRequest : IRequest<PagedResult<TimeEntryResponse>>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    public Guid? EmployeeId { get; init; }

    /// <summary>Başlangıç tarihi (dahil, UTC günü).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Bitiş tarihi (dahil, UTC günü).</summary>
    public DateOnly? To { get; init; }

    internal TimeEntryListQuery ToQuery() =>
        new(new PageQuery { Page = Page, PageSize = PageSize }, EmployeeId, From, To);
}
