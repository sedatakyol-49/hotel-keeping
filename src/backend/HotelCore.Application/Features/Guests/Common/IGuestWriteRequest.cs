using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Guests.Common;

/// <summary>Create ve Update isteklerinin paylaştığı gövde sözleşmesi.</summary>
public interface IGuestWriteRequest
{
    string FirstName { get; }

    string LastName { get; }

    string? Email { get; }

    string? Phone { get; }

    Country? Nationality { get; }

    string? AddressLine { get; }

    string? PostalCode { get; }

    string? City { get; }

    DateOnly? BirthDate { get; }

    string? Culture { get; }

    string? Note { get; }
}
