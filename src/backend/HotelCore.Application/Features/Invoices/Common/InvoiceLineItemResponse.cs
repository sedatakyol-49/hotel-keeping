namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>Fatura satırı.</summary>
public sealed record InvoiceLineItemResponse
{
    public Guid Id { get; init; }

    /// <summary>Satır türü enum <b>adı</b>: <c>RoomCharge | Extra | CityTax</c>.</summary>
    public string Type { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    /// <summary>Birim fiyat — <b>KDV dâhil (brüt)</b>. Bkz. InvoiceAmounts.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Uygulanan KDV oranı (yüzde). Otelin vergi profilinden gelir, istemci belirlemez.</summary>
    public decimal VatRate { get; init; }

    public decimal LineNet { get; init; }

    public decimal LineVat { get; init; }

    /// <summary>Satır brüt tutarı = <c>lineNet + lineVat</c>.</summary>
    public decimal LineGross { get; init; }

    /// <summary>Hizmet tarihi (Leistungsdatum) — GoBD zorunlu alanı.</summary>
    public DateOnly? ServiceDate { get; init; }

    public int SortOrder { get; init; }
}
