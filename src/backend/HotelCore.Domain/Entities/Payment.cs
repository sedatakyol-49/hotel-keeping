using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>Faturaya yapılan ödeme (kısmi ödemeler için çoklu kayıt olabilir).</summary>
public sealed class Payment : EntityBase, ITenantEntity, IAuditableEntity
{
    public Guid HotelId { get; set; }

    public Guid InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public PaymentMethod Method { get; set; } = PaymentMethod.Card;

    public decimal Amount { get; set; }

    public DateTimeOffset PaidAt { get; set; }

    /// <summary>Terminal/havale referansı.</summary>
    public string? Reference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
