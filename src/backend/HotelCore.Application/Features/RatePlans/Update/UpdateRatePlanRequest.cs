using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.RatePlans.Update;

/// <summary><c>PUT /api/v1/rate-plans/{id}</c> gövdesi (tam güncelleme).</summary>
public sealed record UpdateRatePlanRequest : IRequest<RatePlanResponse>, IRatePlanWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public Guid RoomTypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly ValidTo { get; init; }

    public ReservationChannel? Channel { get; init; }

    public bool? IsActive { get; init; }
}
