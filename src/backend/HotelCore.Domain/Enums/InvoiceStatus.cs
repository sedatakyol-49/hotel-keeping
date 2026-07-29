namespace HotelCore.Domain.Enums;

/// <summary>
/// Fatura durumu. <see cref="Finalized"/> sonrası fatura değiştirilemez (GoBD §6.1);
/// düzeltme yalnızca iptal faturası (Stornorechnung) ile yapılır.
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    Finalized = 1,
    Paid = 2,
    Cancelled = 3
}
