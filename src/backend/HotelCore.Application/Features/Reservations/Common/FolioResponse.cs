namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Folio satırı (açık hesap kalemi). Şekli ileride <c>InvoiceLineItem</c> sözleşmesiyle aynıdır;
/// fatura oluşturulunca satırlar aynı alanlarla faturaya taşınır.
/// </summary>
public sealed record FolioLineResponse
{
    public Guid Id { get; init; }

    /// <summary>Satır tipi enum <b>adı</b>: <c>RoomCharge | Extra | CityTax</c>.</summary>
    public string Type { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    /// <summary>Birim brüt fiyat (gösterim için; kesin tutar <c>lineNet + lineVat</c>).</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Satıra uygulanan KDV oranı (yüzde) — otelin vergi profilinden kopyalanmıştır.</summary>
    public decimal VatRate { get; init; }

    public decimal LineNet { get; init; }

    public decimal LineVat { get; init; }

    /// <summary>Brüt satır tutarı = <c>lineNet + lineVat</c>.</summary>
    public decimal LineGross { get; init; }

    /// <summary>Hizmet tarihi (Leistungsdatum) — GoBD zorunlu alanı.</summary>
    public DateOnly? ServiceDate { get; init; }
}

/// <summary>
/// <c>GET /api/v1/reservations/{id}/folio</c> yanıtı: açık hesabın satırları + toplamları.
/// <para>
/// Fatura henüz yoktur; folio check-out'tan sonra da <b>açık</b> kalır ve faturalama modülü
/// tarafından kapatılır (architecture.md §5).
/// </para>
/// </summary>
public sealed record FolioResponse
{
    public Guid ReservationId { get; init; }

    public string ReservationNumber { get; init; } = string.Empty;

    /// <summary>Folio henüz açılmamışsa <c>null</c> (satır listesi de boş olur).</summary>
    public Guid? FolioId { get; init; }

    public bool IsClosed { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string GuestName { get; init; } = string.Empty;

    public IReadOnlyList<FolioLineResponse> Lines { get; init; } = [];

    /// <summary>Satırların net toplamı.</summary>
    public decimal TotalNet { get; init; }

    /// <summary>Satırların KDV toplamı.</summary>
    public decimal TotalVat { get; init; }

    /// <summary>Satırların brüt toplamı (misafirin ödeyeceği açık bakiye).</summary>
    public decimal TotalGross { get; init; }
}
