namespace HotelCore.Domain.Enums;

/// <summary>Fatura denetim izi aksiyonu (append-only, GoBD §6.3).</summary>
public enum InvoiceAuditAction
{
    Created = 0,
    Finalized = 1,
    Paid = 2,
    Cancelled = 3
}
