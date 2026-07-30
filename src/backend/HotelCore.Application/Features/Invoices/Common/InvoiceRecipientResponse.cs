namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Faturanın alıcısı (<i>Leistungsempfänger</i>) — misafirin adı ve adresi.
/// <para>
/// <b>Hukuki dayanak:</b> UStG §14 Abs. 4 Nr. 1 "der vollständige Name und die vollständige
/// Anschrift ... des Leistungsempfängers". Alıcının <b>tam</b> adı ve adresi zorunlu içeriktir;
/// eksikse alıcı KDV indirimini (Vorsteuerabzug, UStG §15 Abs. 1) kaybedebilir.
/// </para>
/// <para>
/// <b>İki bilinen eksik (şema ihtiyacı olarak raporlandı, burada uydurulmaz):</b>
/// <list type="bullet">
///   <item><b>Adresin ülkesi yoktur.</b> <c>Guest</c> üzerinde yalnızca
///   <c>AddressLine/PostalCode/City</c> vardır. <c>Guest.Nationality</c> <i>uyrukluktur</i>, adres
///   ülkesi <b>değildir</b> — buraya eşlenmesi belgeye yanlış bilgi yazmak olurdu, bu yüzden
///   eşlenmemiştir.</item>
///   <item><b>Adres alanları zorunlu değildir</b> ve finalize sırasında doluluk denetimi yoktur:
///   adresi olmayan misafir için §14 Abs. 4 Nr. 1 sağlanamayan bir belge kesilebilir
///   (250 € altı için §33 UStDV basitleştirmesi hariç).</item>
/// </list>
/// </para>
/// <para>
/// Değerler <b>güncel</b> misafir kaydından okunur; belge anındaki adres ayrıca saklanmaz.
/// </para>
/// </summary>
public sealed record InvoiceRecipientResponse
{
    public Guid GuestId { get; init; }

    /// <summary>Ad + soyad (<c>Guest.FirstName + " " + Guest.LastName</c>).</summary>
    public string Name { get; init; } = string.Empty;

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }
}
