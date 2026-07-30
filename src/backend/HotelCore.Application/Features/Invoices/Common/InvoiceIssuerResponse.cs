namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Faturayı düzenleyen (<i>Rechnungsaussteller</i>) — otel künyesi.
/// <para>
/// <b>Hukuki dayanak:</b> UStG §14 Abs. 4 Nr. 1 "der vollständige Name und die vollständige
/// Anschrift des leistenden Unternehmers" ve Nr. 2 "die dem leistenden Unternehmer erteilte
/// Steuernummer oder Umsatzsteuer-Identifikationsnummer". Bu alanlar belge üzerinde
/// <b>zorunludur</b>; API bunları döndürmezse PDF/ZUGFeRD üretimi de üretemez.
/// </para>
/// <para>
/// <b>Uyarı — tek alan, iki kavram:</b> <see cref="TaxNumber"/> domainde tek bir serbest metin
/// kolonudur (<c>Hotel.TaxNumber</c>). §14 Abs. 4 Nr. 2 <i>Steuernummer</i> <b>veya</b>
/// <i>USt-IdNr.</i> ister; hangisinin girildiği ayırt edilemediği için belge üzerinde doğru etiketle
/// basılamaz (AB içi hizmetlerde ayrıca <b>USt-IdNr. zorunludur</b>, §14a UStG). Şema ihtiyacı
/// olarak raporlanmıştır.
/// </para>
/// <para>
/// Değerler <b>fatura anındaki</b> değil <b>güncel</b> otel künyesidir: kesinleşmiş belgenin
/// künyesi ayrıca saklanmaz (bkz. şema ihtiyacı raporu). Otel adresi değişirse eski faturalar
/// yeni adresle görünür — GoBD <i>Unveränderbarkeit</i> açısından mali müşavir onayı gerektiren
/// bir noktadır.
/// </para>
/// </summary>
public sealed record InvoiceIssuerResponse
{
    public Guid HotelId { get; init; }

    /// <summary>Otelin tam adı (<c>Hotel.Name</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Sokak/kapı no. Otel künyesinde boş olabilir → belge eksik olur.</summary>
    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string City { get; init; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 ülke kodu (enum adı: <c>DE</c>, <c>AT</c>, <c>TR</c> ...).</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>Steuernummer <b>veya</b> USt-IdNr. (ayrım domainde yok — bkz. tip belgesi).</summary>
    public string? TaxNumber { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }
}
