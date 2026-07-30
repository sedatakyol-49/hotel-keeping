using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.List;

/// <summary><c>GET /api/v1/guests</c> — sayfalı + aranabilir misafir listesi.</summary>
public sealed record ListGuestsRequest : IRequest<PagedResult<GuestResponse>>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    /// <summary>Ad, soyad ve e-postada "contains" arama (büyük/küçük harf duyarsız).</summary>
    public string? Search { get; init; }

    internal GuestListQuery ToQuery() =>
        new(new PageQuery { Page = Page, PageSize = PageSize }, Search);
}
