using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Misafirin rezervasyon sırasında <b>isteğe bağlı</b> olarak verdiği fatura künyesi —
/// <b>owned type</b> (<c>PublicBookings</c> tablosunda <c>InvoiceAddress_*</c> kolonları).
///
/// <para><b>Neden <see cref="Guest"/> üzerinde değil:</b> architecture-public-booking.md §7
/// "<c>Guest</c> şeması değişmez" der, ama sözleşme §6.2 <c>invoiceAddress.company</c> ve
/// <c>invoiceAddress.vatId</c> alanlarını doğrular — <see cref="Guest"/>'te ne şirket adı ne
/// USt-IdNr. kolonu vardır. Blok bir bütün olarak burada donar; adres <b>bileşenleri</b> ayrıca
/// <see cref="Guest"/>'e kopyalanabilir, ama fatura künyesinin <i>beyan edildiği hâli</i>
/// rezervasyonun kanıtına aittir ve misafir kaydı sonradan düzenlense bile değişmemelidir.</para>
///
/// <para><b>Zorunlu değildir</b> (§33 UStDV: küçük tutarlı faturada alıcı künyesi aranmaz);
/// yalnızca kurumsal fatura isteyen misafir doldurur — veri minimizasyonu.</para>
/// </summary>
public sealed class PublicInvoiceAddress
{
    /// <summary>Şirket / kurum adı.</summary>
    public string? Company { get; set; }

    public string? AddressLine { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public Country? Country { get; set; }

    /// <summary>Alıcının USt-IdNr.'si (§14a UStG — AB içi ters yükümlülük senaryoları).</summary>
    public string? VatId { get; set; }

    /// <summary>Blok gerçekten dolduruldu mu (tüm alanlar boşsa misafir kurumsal fatura istemedi).</summary>
    public bool HasValue =>
        !string.IsNullOrWhiteSpace(Company)
        || !string.IsNullOrWhiteSpace(AddressLine)
        || !string.IsNullOrWhiteSpace(PostalCode)
        || !string.IsNullOrWhiteSpace(City)
        || Country is not null
        || !string.IsNullOrWhiteSpace(VatId);
}
