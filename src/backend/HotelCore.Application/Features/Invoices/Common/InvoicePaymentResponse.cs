namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>Faturaya kaydedilmiş ödeme.</summary>
public sealed record InvoicePaymentResponse
{
    public Guid Id { get; init; }

    /// <summary>Ödeme yöntemi enum <b>adı</b>: <c>Cash | Card | Transfer</c>.</summary>
    public string Method { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTimeOffset PaidAt { get; init; }

    /// <summary>Terminal/havale referansı (opsiyonel).</summary>
    public string? Reference { get; init; }
}
