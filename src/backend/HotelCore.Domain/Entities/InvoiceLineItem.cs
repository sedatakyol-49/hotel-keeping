using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Fatura/folio satırı. Konaklama sürerken satır folioya bağlıdır (<see cref="FolioId"/>),
/// faturalandığında <see cref="InvoiceId"/> dolar. Fatura finalize edildikten sonra satırlar
/// değiştirilemez (SaveChanges guard).
/// </summary>
public sealed class InvoiceLineItem : EntityBase, ITenantEntity
{
    public Guid HotelId { get; set; }

    public Guid? InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public Guid? FolioId { get; set; }

    public Folio? Folio { get; set; }

    public InvoiceLineType Type { get; set; } = InvoiceLineType.RoomCharge;

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    /// <summary>Satıra uygulanan KDV oranı, yüzde (otelin TaxProfile değerinden kopyalanır).</summary>
    public decimal VatRate { get; set; }

    public decimal LineNet { get; set; }

    public decimal LineVat { get; set; }

    /// <summary>Hizmet tarihi (Leistungsdatum) — GoBD zorunlu alanı.</summary>
    public DateOnly? ServiceDate { get; set; }

    public int SortOrder { get; set; }
}
