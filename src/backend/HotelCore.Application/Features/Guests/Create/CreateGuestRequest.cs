using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Guests.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Guests.Create;

/// <summary><c>POST /api/v1/guests</c> gövdesi.</summary>
public sealed record CreateGuestRequest : IRequest<GuestResponse>, IGuestWriteRequest
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Uyruk — <c>Country</c> enum adı (örn. <c>DE</c>).</summary>
    public Country? Nationality { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public DateOnly? BirthDate { get; init; }

    /// <summary>Yazışma/fatura dili (<c>de|en|tr</c>).</summary>
    public string? Culture { get; init; }

    public string? Note { get; init; }
}
