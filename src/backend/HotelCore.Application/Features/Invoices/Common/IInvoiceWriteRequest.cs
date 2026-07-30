namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>Fatura yazma isteklerinin (Create/Update) ortak yüzeyi — paylaşılan doğrulama için.</summary>
public interface IInvoiceWriteRequest
{
    /// <summary>Fatura dili (<c>de|en|tr</c>); verilmezse misafir → otel varsayılanına düşülür.</summary>
    string? Culture { get; }

    /// <summary>Fatura satırları.</summary>
    IReadOnlyList<InvoiceLineInput> LineItems { get; }
}
