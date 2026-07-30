using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Guests.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Guests.Update;

/// <summary><c>PUT /api/v1/guests/{id}</c> gövdesi (tam güncelleme).</summary>
public sealed record UpdateGuestRequest : IRequest<GuestResponse>, IGuestWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public Country? Nationality { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public DateOnly? BirthDate { get; init; }

    public string? Culture { get; init; }

    public string? Note { get; init; }
}
