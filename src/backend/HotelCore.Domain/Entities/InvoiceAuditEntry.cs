using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Fatura denetim izi (GoBD §6.3) — <b>append-only</b>: güncellenmez, silinmez.
/// </summary>
public sealed class InvoiceAuditEntry : EntityBase, ITenantEntity
{
    public Guid HotelId { get; set; }

    public Guid InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public InvoiceAuditAction Action { get; set; }

    public Guid? PerformedByUserId { get; set; }

    public DateTimeOffset PerformedAt { get; set; }

    /// <summary>Serbest biçimli JSON ayrıntı (değişen alanlar, tutarlar).</summary>
    public string? Details { get; set; }
}
