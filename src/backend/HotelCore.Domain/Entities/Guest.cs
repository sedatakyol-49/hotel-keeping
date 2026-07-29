using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Misafir. Geçmiş konaklama sayısı hesaplanan bir değerdir (rezervasyonlardan türetilir),
/// bu yüzden kolon olarak tutulmaz.
/// </summary>
public sealed class Guest : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public Country? Nationality { get; set; }

    public string? AddressLine { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public DateOnly? BirthDate { get; set; }

    /// <summary>Fatura ve yazışma dili.</summary>
    public string? Culture { get; set; }

    public string? Note { get; set; }

    public ICollection<Reservation> Reservations { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
