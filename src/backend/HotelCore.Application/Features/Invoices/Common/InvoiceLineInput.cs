using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Elle girilen fatura satırı (POST/PUT gövdesi).
/// <para>
/// İstemci <b>yalnızca</b> tür, açıklama, miktar, brüt birim fiyat ve hizmet tarihini verir.
/// <c>vatRate</c>, <c>lineNet</c>, <c>lineVat</c> gibi alanlar bilinçli olarak <b>yoktur</b>:
/// KDV oranı otelin <c>TaxProfile</c>'ından çözülür, tutarlar sunucuda hesaplanır. Aksi hâlde
/// istemci vergi matrahını manipüle edebilirdi.
/// </para>
/// </summary>
public sealed record InvoiceLineInput
{
    /// <summary>Satır türü: <c>RoomCharge | Extra | CityTax</c>.</summary>
    public InvoiceLineType Type { get; init; } = InvoiceLineType.Extra;

    public string Description { get; init; } = string.Empty;

    public decimal Quantity { get; init; } = 1m;

    /// <summary>Birim fiyat — <b>KDV dâhil (brüt)</b>.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Hizmet tarihi (Leistungsdatum). Verilmezse fatura oluşturma günü kullanılır.</summary>
    public DateOnly? ServiceDate { get; init; }
}
