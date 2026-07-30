using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Update;

/// <summary>
/// <c>PUT /api/v1/reservations/{id}</c> — tarih / oda / kişi / kanal değişikliği.
/// <para>
/// <b>Durum bu uçtan değiştirilemez</b> (check-in, check-out, iptal, no-show ayrı uçlardır) ve
/// <c>totalAmount</c> yine sunucuda <b>yeniden hesaplanır</b>.
/// </para>
/// </summary>
public sealed record UpdateReservationRequest : IRequest<ReservationResponse>, IReservationWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public Guid RoomId { get; init; }

    public Guid GuestId { get; init; }

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Adults { get; init; } = 1;

    public int Children { get; init; }

    public ReservationChannel Channel { get; init; } = ReservationChannel.Direct;

    public decimal DepositPercent { get; init; }

    public string? Notes { get; init; }
}
