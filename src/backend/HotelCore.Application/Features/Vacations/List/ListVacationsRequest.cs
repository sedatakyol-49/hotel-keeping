using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Vacations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Vacations.List;

/// <summary>
/// <c>GET /api/v1/vacations</c> — sayfalı + filtreli izin talebi listesi.
/// </summary>
public sealed record ListVacationsRequest : IRequest<PagedResult<VacationRequestResponse>>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    public Guid? EmployeeId { get; init; }

    public VacationStatus? Status { get; init; }

    /// <summary>Takvim yılı; talep aralığı o yılla kesişiyorsa listelenir.</summary>
    public int? Year { get; init; }

    /// <summary>Aralık başlangıcı — bu tarihten önce bitmiş izinler listelenmez.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Aralık bitişi — bu tarihten sonra başlayan izinler listelenmez.</summary>
    public DateOnly? To { get; init; }

    internal VacationListQuery ToQuery() =>
        new(
            new PageQuery { Page = Page, PageSize = PageSize },
            EmployeeId,
            Status,
            Year,
            From,
            To);
}
