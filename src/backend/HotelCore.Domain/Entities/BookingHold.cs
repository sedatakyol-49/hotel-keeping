using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Misafir kanalındaki <b>15 dakikalık geçici tutma</b>: somut bir odayı, donmuş bir teklifle
/// birlikte, rezervasyon tamamlanana kadar rezerve eder (architecture-public-booking.md §5.2).
///
/// <para><b>Neden <c>Reservation.Status = Option</c> değil:</b> <c>Option</c> ticari ve
/// operasyonel bir durumdur — rezervasyon numarası tüketir, doluluk grid'inde görünür, raporlara
/// ve folio'ya girer, bir <see cref="Guest"/> kaydı ister. Terk edilmiş sepetler resepsiyonun
/// takvimini kirletir ve DSGVO açısından henüz gerekmeyen kişisel veriyi erkenden yaratır.</para>
///
/// <para><b>Kişisel veri taşımaz.</b> Ad, e-posta, telefon <b>yoktur</b>: hold aşamasında misafir
/// henüz hiçbir şey beyan etmemiştir (veri minimizasyonu, DSGVO Art. 5 Abs. 1 lit. c). Tek
/// kimlik izi <see cref="ClientIpHash"/>'tir ve o da <b>tuzlanmış</b> özettir — kötüye kullanım
/// tespiti dışında hiçbir amaçla okunamaz.</para>
///
/// <para><b>Kart verisi taşımaz</b> ve taşımayacaktır (PCI-DSS kapsam dışılığı,
/// architecture-public-booking.md §6.2).</para>
///
/// <para><b>Neden <see cref="ISoftDeletable"/> DEĞİL — bu bir tasarım kararıdır, unutma değil:</b>
/// süresi dolmuş hold <b>fiziksel olarak</b> silinir. Çakışma kısıtının
/// (<c>EX_BookingHolds_NoOverlappingActiveHolds</c>) kısmi predikatı <b>immutable</b> olmak
/// zorundadır, yani içinde <c>now()</c> geçemez; dolayısıyla "süresi dolmuş" hâli predikatla
/// ifade edilemez. Soft-delete edilseydi silinmiş satırlar predikatta kalmaya devam eder ve odayı
/// sonsuza dek bloke ederdi. Saklanacak bir hukuki/mali değeri de yoktur (rezervasyona dönüşen
/// hold'un tüm kanıtı <see cref="PublicBooking"/>'e kopyalanır).</para>
/// </summary>
public sealed class BookingHold : EntityBase, ITenantEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    /// <summary>Misafirin seçtiği oda tipi — satış oda tipi bazındadır.</summary>
    public Guid RoomTypeId { get; set; }

    public RoomType RoomType { get; set; } = null!;

    /// <summary>
    /// Sunucunun <b>deterministik</b> olarak pinlediği somut oda (<c>floor</c> ↑, sonra
    /// <c>number</c> ↑ ilk uygun oda). <c>Reservation.RoomId</c> zorunlu olduğu için oda seçimi
    /// rezervasyona değil hold'a ertelenemez; ayrıca pinlenmeden iki eşzamanlı istek aynı odayı
    /// seçerdi.
    /// </summary>
    public Guid RoomId { get; set; }

    public Room Room { get; set; } = null!;

    /// <summary>Giriş günü (dahil) — yarı açık aralık <c>[CheckIn, CheckOut)</c>.</summary>
    public DateOnly CheckIn { get; set; }

    /// <summary>Çıkış günü (dahil değil).</summary>
    public DateOnly CheckOut { get; set; }

    public int Adults { get; set; } = 1;

    public int Children { get; set; }

    /// <summary>
    /// Hold token'ının <b>SHA-256 özeti</b>. Ham token yalnızca yanıtta döner ve saklanmaz —
    /// mevcut <see cref="RefreshToken"/> deseninin aynısı: veritabanı sızarsa token'lar
    /// kullanılamaz olur.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Hold'un sona erdiği mutlak an (oluşturma + 15 dakika).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Oluşturma anı — süpürücü servis ve teşhis için.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Hold rezervasyona dönüştüyse tüketilme anı. <c>null</c> olduğu sürece hold çakışma
    /// kısıtının kapsamındadır; dolduğunda odayı artık rezervasyonun kendisi bloke eder.
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Hold'u tüketen rezervasyon (teşhis ve destek için geri referans).</summary>
    public Guid? ConsumedByReservationId { get; set; }

    public Reservation? ConsumedByReservation { get; set; }

    /// <summary>
    /// Tuzlanmış istemci IP özeti (kötüye kullanım analizi). Ham IP <b>saklanmaz</b>; tuz
    /// yapılandırmadan gelir, böylece özet başka veri kümeleriyle eşleştirilemez.
    /// </summary>
    public string? ClientIpHash { get; set; }

    // ---------------------------------------------------------------------------------------
    // Dondurulmuş teklif — §312j Abs. 2 BGB
    // ---------------------------------------------------------------------------------------

    /// <summary>Teklifin para birimi (otelin para biriminin anlık kopyası).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Dondurulmuş konaklama brüt tutarı. <b>Neden JSON'un yanında ayrıca kolon:</b> bu tutar
    /// <c>Reservation.TotalAmount</c>'a ve iptal ücreti matrahına <i>hesap olarak</i> girer;
    /// para matematiğinin girdisini bir JSON metninden ayrıştırmak, tipi ve ondalık kesinliği
    /// serileştirme biçimine bağlar.
    /// </summary>
    public decimal AccommodationGross { get; set; }

    /// <summary>Dondurulmuş Kurtaxe tutarı (KDV dışı, <i>durchlaufender Posten</i>).</summary>
    public decimal CityTaxAmount { get; set; }

    /// <summary>PAngV Gesamtpreis = <see cref="AccommodationGross"/> + <see cref="CityTaxAmount"/>.</summary>
    public decimal TotalGross { get; set; }

    /// <summary>Public fiyat nesnesinin (<c>PublicPrice</c>) dondurulmuş kopyası.</summary>
    public string PriceSnapshotJson { get; set; } = string.Empty;

    /// <summary>İptal politikasının dondurulmuş kopyası (mutlak son tarih dâhil).</summary>
    public string CancellationPolicySnapshotJson { get; set; } = string.Empty;

    /// <summary>§312j Abs. 2 zorunlu özetinin dondurulmuş kopyası.</summary>
    public string OrderSummaryJson { get; set; } = string.Empty;

    /// <summary>
    /// <c>orderSummary</c>'nin kanonik JSON SHA-256'sı (<c>sha256:</c> + 64 küçük harf hex).
    /// Rezervasyon isteği bunu geri gönderir; uyuşmazsa <c>409 SUMMARY_CHANGED</c> — §312j
    /// Abs. 2'nin makineyle zorlanabilir kısmı.
    /// </summary>
    public string SummaryHash { get; set; } = string.Empty;

    /// <summary>Hukuki bildirimlerin ve versiyonlarının dondurulmuş kopyası.</summary>
    public string LegalSnapshotJson { get; set; } = string.Empty;

    /// <summary>Teklifin üretildiği dil — donmuş metinler bu dilde yazılmıştır.</summary>
    public string Culture { get; set; } = "de";

    /// <summary>Hold hâlâ geçerli mi (tüketilmemiş ve süresi dolmamış). Kolon olarak tutulmaz.</summary>
    /// <param name="now">Değerlendirme anı.</param>
    public bool IsActiveAt(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
}
