using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Bir <see cref="Reservation"/>'ın <b>public kanal yüzü</b>: misafirin gördüğü referans, erişim
/// kimlik bilgisi ve — asıl önemlisi — <b>rızaların ve hukuki metinlerin dondurulmuş anlık
/// görüntüsü</b>. Rezervasyonla bire-birdir.
///
/// <para><b>Neden <see cref="Reservation"/> üzerine kolon eklenmedi:</b> bu alanların tamamı
/// yalnızca web satışında vardır. Resepsiyondan girilen her rezervasyonda 20+ boş kolon taşımak,
/// "rıza alınmamış" ile "rıza sorulmamış" ayrımını da yok ederdi — <c>NULL</c> her ikisini de
/// ifade eder, satırın <i>yokluğu</i> ise yalnızca ikincisini.</para>
///
/// <para><b>Kanıt değeri:</b> uyuşmazlıkta otelin elindeki tek belge budur — hangi metnin hangi
/// versiyonu onaylandı (DSGVO Art. 7 Abs. 1), düğmede hangi metin gösterildi (§312j Abs. 3),
/// düğmenin üstünde hangi özet duruyordu (§312j Abs. 2), hangi fiyat ve politika taahhüt edildi.
/// Bu yüzden alanlar <b>anlık görüntüdür</b>: otel yarın AGB'sini veya fiyatını değiştirdiğinde
/// geçmiş rezervasyonun kanıtı değişmez.</para>
///
/// <para><b>Kart verisi YOKTUR</b> ve eklenmeyecektir (architecture-public-booking.md §6.2).
/// <b>Meldeschein verisi de yoktur</b> (doğum tarihi, uyrukluk, kimlik no, imza): o veri girişte
/// alınır (BMG §§29–30), rezervasyon anında toplanması amaç sınırlamasına aykırıdır.</para>
/// </summary>
public sealed class PublicBooking : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid ReservationId { get; set; }

    public Reservation Reservation { get; set; } = null!;

    /// <summary>
    /// Misafire gösterilen referans — <b>Crockford Base32, 12 karakter (60 bit)</b>, tireler
    /// olmadan normalize saklanır (<c>K7QM3XPD9RTV</c>); gösterim <c>4-4-4</c> gruplamasını
    /// sunum katmanı ekler.
    /// <para>
    /// <b><c>ReservationNumber</c> (<c>RES-2026-00042</c>) neden kullanılamaz:</b> sıralı ve
    /// tahmin edilebilirdir; sorgulama anahtarı yapılırsa saldırgan tüm rezervasyonları sırayla
    /// dener. Crockford alfabesi <c>I/L/O/U</c> içermez — telefonda hatasız dikte edilir,
    /// <c>0/O</c> ve <c>1/I</c> karışmaz, kazara küfür üretmez.
    /// </para>
    /// <para>
    /// <b>Tek başına veri döndürmez:</b> taşıyıcı kimlik bilgisi <see cref="AccessTokenHash"/>'in
    /// ham hâlidir; referans yalnızca <c>lookup</c> ucunda e-postayla birlikte kullanılır.
    /// </para>
    /// </summary>
    public string BookingReference { get; set; } = string.Empty;

    /// <summary>
    /// Erişim token'ının SHA-256 özeti (base64url, 27 karakter / 160 bit ham token).
    /// Ham değer <b>yalnızca</b> oluşturma yanıtında ve onay e-postasındaki bağlantıda görünür;
    /// veritabanında saklanmaz. Karşılaştırma <b>sabit zamanlı</b> yapılmalıdır.
    /// </summary>
    public string AccessTokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Self-servis erişimin kapandığı an (<c>CheckOut</c> + 30 gün). Sonrasında uçlar
    /// <b>404</b> döner; <b>veri silinmez</b> — faturalanmış konaklama GoBD/AO §147 gereği
    /// 10 yıl saklanır. Kapanan şey erişimdir, kayıt değil.
    /// </summary>
    public DateTimeOffset AccessTokenExpiresAt { get; set; }

    /// <summary>Misafirin yazışma/fatura dili (rezervasyon anındaki seçimi).</summary>
    public string Culture { get; set; } = "de";

    /// <summary>
    /// Beyan edilen ikamet ülkesi (opsiyonel).
    /// <para>
    /// <b>Neden <c>Guest.Nationality</c> değil:</b> ikamet ülkesi ile <b>uyrukluk</b> farklı
    /// verilerdir. Uyrukluk Meldeschein verisidir ve rezervasyonda sorulmaz (§9.6);
    /// <c>Guest.Nationality</c> bilinçli olarak <c>null</c> bırakılır. Sözleşme §6.1'deki
    /// <c>countryOfResidence</c> alanının başka bir yeri yoktur.
    /// </para>
    /// </summary>
    public Country? CountryOfResidence { get; set; }

    /// <summary>
    /// Misafirin bildirdiği tahmini geliş saati (otel yerel saati, opsiyonel).
    /// <see cref="Reservation"/>'da böyle bir alan yoktur; operasyonel bir <i>beyandır</i>,
    /// rezervasyonun kendisinin bir özelliği değildir.
    /// </summary>
    public TimeOnly? EstimatedArrivalLocalTime { get; set; }

    /// <summary>Opsiyonel kurumsal fatura künyesi — owned type.</summary>
    public PublicInvoiceAddress InvoiceAddress { get; set; } = new();

    // ---------------------------------------------------------------------------------------
    // Rızalar — DSGVO Art. 7 Abs. 1 (hesap verebilirlik)
    // ---------------------------------------------------------------------------------------

    /// <summary>AGB kabul edildi (uygulama katmanı <c>false</c> ile rezervasyon oluşturmaz).</summary>
    public bool TermsAccepted { get; set; }

    /// <summary>Kabul edilen AGB versiyonu — <see cref="HotelLegalDocument.Version"/> ile eşleşir.</summary>
    public string? TermsVersion { get; set; }

    /// <summary>DSGVO Art. 13 aydınlatma metni okundu bildirimi.</summary>
    public bool PrivacyNoticeAcknowledged { get; set; }

    public string? PrivacyNoticeVersion { get; set; }

    /// <summary>
    /// §312g Abs. 2 Nr. 9 BGB — <b>cayma hakkının bulunmadığı</b> bildiriminin okunduğu beyanı.
    /// Tarihli konaklama sözleşmelerinde yasal cayma hakkı yoktur, ama bunun <i>bildirilmesi</i>
    /// gerekir; genel bir Widerrufsbelehrung göstermek yanıltıcı olurdu.
    /// </summary>
    public bool WithdrawalNoticeAcknowledged { get; set; }

    public string? WithdrawalNoticeVersion { get; set; }

    /// <summary>18+ beyanı (§§104 ff. BGB — beyanın hukuki değeri sınırlıdır, kanıt olarak tutulur).</summary>
    public bool BookerIsAdult { get; set; }

    /// <summary>
    /// Pazarlama izni. <b>Ön işaretli olamaz</b> (DSGVO Art. 4 Nr. 11): varsayılan <c>false</c>
    /// ve rezervasyon <c>false</c> ile de tamamlanır.
    /// </summary>
    public bool MarketingOptIn { get; set; }

    /// <summary>Rızaların alındığı an (kanıtın zaman damgası).</summary>
    public DateTimeOffset ConsentRecordedAt { get; set; }

    // ---------------------------------------------------------------------------------------
    // §312j kanıtları ve dondurulmuş teklif
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// İstemcinin gösterdiğini bildirdiği sipariş düğmesi metni (§312j Abs. 3).
    /// <b>Sunucu bu metni doğrulamaz, kaydeder</b> — dil/varyant meşru olabilir; sunucunun
    /// istemci ekranını görmesi mümkün değildir. Yapılabilecek tek şey kanıt saklamaktır.
    /// </summary>
    public string? OrderButtonLabel { get; set; }

    /// <summary>Onaylanan özetin hash'i (<c>sha256:</c> + 64 hex) — hold'daki değerle aynıdır.</summary>
    public string SummaryHash { get; set; } = string.Empty;

    /// <summary>§312j Abs. 2 zorunlu özetinin dondurulmuş kopyası.</summary>
    public string OrderSummaryJson { get; set; } = string.Empty;

    /// <summary>Rezervasyon anındaki fiyat nesnesinin dondurulmuş kopyası.</summary>
    public string PriceSnapshotJson { get; set; } = string.Empty;

    /// <summary>Rezervasyon anındaki iptal politikasının dondurulmuş kopyası.</summary>
    public string CancellationPolicySnapshotJson { get; set; } = string.Empty;

    /// <summary>Hukuki bildirimlerin (cayma, düğme, AGB/aydınlatma versiyonları) dondurulmuş kopyası.</summary>
    public string LegalSnapshotJson { get; set; } = string.Empty;

    /// <summary>Sözleşmenin kurulma modeli — rezervasyon anındaki otel ayarının kopyası.</summary>
    public PublicBookingConfirmationMode ConfirmationMode { get; set; } =
        PublicBookingConfirmationMode.Instant;

    // ---------------------------------------------------------------------------------------
    // §312f BGB — kalıcı veri taşıyıcısında onay
    // ---------------------------------------------------------------------------------------

    /// <summary>Onay e-postasının gönderildiği an; outbox gönderimi başarılı olunca dolar.</summary>
    public DateTimeOffset? ConfirmationSentAt { get; set; }

    /// <summary>Gönderilen onay belgesinin SHA-256 özeti — "ne gönderildi" sorusunun kanıtı.</summary>
    public string? ConfirmationDocumentHash { get; set; }

    /// <summary>Onay belgesinin şablon versiyonu.</summary>
    public string? ConfirmationDocumentVersion { get; set; }

    /// <summary>Onay belgesinin dili.</summary>
    public string? ConfirmationCulture { get; set; }

    // ---------------------------------------------------------------------------------------
    // Online iptal
    // ---------------------------------------------------------------------------------------

    /// <summary>Misafirin <b>online</b> iptal ettiği an (resepsiyon iptali burayı doldurmaz).</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>
    /// İptalde misafire bildirilen ve onayladığı ücret. Matrah <b>yalnızca konaklama
    /// tutarıdır</b>; Kurtaxe girmez (konaklama gerçekleşmediği için vergi doğmaz).
    /// <b>Tahsilat ve faturalama bu alanla yapılmaz</b> — public uç yalnızca tutarı bildirir,
    /// otelin mevcut faturalama akışı karar verir.
    /// </summary>
    public decimal? CancellationFeeAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
