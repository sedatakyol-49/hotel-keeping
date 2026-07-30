using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.List;

/// <summary>
/// <c>GET /api/v1/reservations</c> — sayfalı + filtreli rezervasyon listesi.
/// </summary>
public sealed record ListReservationsRequest : IRequest<PagedResult<ReservationResponse>>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    public ReservationStatus? Status { get; init; }

    public ReservationChannel? Channel { get; init; }

    public Guid? RoomId { get; init; }

    public Guid? GuestId { get; init; }

    /// <summary>Aralık başlangıcı — konaklaması bu günden sonra biten kayıtlar.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Aralık bitişi (dahil değil) — konaklaması bu günden önce başlayan kayıtlar.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Rezervasyon numarası veya misafir ad/soyadında "contains" arama.</summary>
    public string? Search { get; init; }

    internal ReservationListQuery ToQuery() =>
        new(
            new PageQuery { Page = Page, PageSize = PageSize },
            Status,
            Channel,
            RoomId,
            GuestId,
            From,
            To,
            Search);
}
