using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Folio — konaklama boyunca açık hesap. Masraflar <see cref="InvoiceLineItem"/> olarak eklenir;
/// check-out'ta bu satırlar bir <see cref="Invoice"/> altına taşınır ve folio kapatılır.
/// Rezervasyon ile bire-bir ilişkilidir.
/// </summary>
public sealed class Folio : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid ReservationId { get; set; }

    public Reservation Reservation { get; set; } = null!;

    public bool IsClosed { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<InvoiceLineItem> LineItems { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
