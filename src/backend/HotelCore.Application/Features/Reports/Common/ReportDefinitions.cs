namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Raporlama modülünün <b>tanım sözlüğü</b>. Bu modülün asıl zorluğu kod değil, metriklerin
/// tutarlı tanımlanmasıdır; bu yüzden tüm tanımlar tek dosyada toplanır ve
/// <c>docs/api-contracts-reports.md</c> ile birebir aynıdır.
///
/// <para><b>1) Dönem — kapalı gün aralığı <c>[from, to]</c>.</b>
/// Rezervasyon/müsaitlik modülü <i>yarı açık</i> <c>[checkIn, checkOut)</c> kullanır çünkü orada
/// birim <b>gece</b>dir. Rapor ise bir <b>gün kümesi</b> üzerinde konuşur ("1–7 Ağustos ciro"),
/// bu yüzden her iki uç da dâhildir ve <c>to == from</c> tek günlük rapor demektir. İkisi şöyle
/// bağlanır: rapor, <b>başlangıç günü aralıkta olan geceleri</b> sayar. Yani gece penceresi
/// yarı açık <c>[from, to + 1 gün)</c>'dir ve rezervasyon modülünün kararına <b>birebir uyar</b>:
/// çıkış günü gece saymaz.</para>
///
/// <para><b>2) Satılan oda-gece (sold room nights).</b> Odayı bloke eden bir rezervasyonun
/// (durum <c>Cancelled</c> ve <c>NoShow</c> DEĞİL — <c>AvailabilityQuery.IsBlocking</c> ile aynı
/// kural) rapor penceresiyle kesişen gece sayısı. Her rezervasyon tek bir odaya bağlıdır, bu
/// yüzden "gece sayısı" = "oda-gece". <c>NoShow</c> sayılmaz çünkü misafir konaklamamıştır;
/// gelmeyene kesilen iptal bedeli ADR'yi bozmasın diye ayrı alanda raporlanır
/// (<c>otherInvoicedRevenue</c>).</para>
///
/// <para><b>3) Fiziksel / servis dışı / müsait oda-gece.</b>
/// <c>physicalRoomNights = oda sayısı × gün sayısı</c>,
/// <c>outOfOrderRoomNights = servis dışı oda sayısı × gün sayısı</c>,
/// <c>availableRoomNights = physical − outOfOrder</c>.
/// <b>Karar:</b> servis dışı (<c>IsOutOfOrder</c>) odalar müsait kapasiteden <b>düşülür</b> —
/// otelcilik pratiğinde tadilattaki/arızalı oda satılabilir envanter değildir; kapasiteye dâhil
/// edilirse doluluk oranı yapay olarak düşer ve müdürün elinde olmayan bir sebep performans gibi
/// görünür. Üç sayı da yanıtta <b>ayrı ayrı</b> döndüğü için tüketici isterse kendi tanımını
/// (örn. fiziksel kapasiteye göre doluluk) yeniden kurabilir.</para>
///
/// <para><b>4) Doluluk oranı</b> = <c>satılan / müsait × 100</c> (2 ondalık). Müsait oda-gece 0
/// ise 0 döner (bölme yapılmaz). <b>%100'ü aşabilir:</b> <c>IsOutOfOrder</c> tarihsiz bir
/// <i>anlık durum</i> bayrağıdır (bkz. 8); bugün servis dışına alınan bir oda geçmişte dolu
/// olabilir. Bu değer kırpılmaz — gerçek gizlenmez.</para>
///
/// <para><b>5) ADR (Average Daily Rate)</b> = <c>oda geliri / satılan oda-gece</c>.
/// Paya <b>yalnızca konaklama geliri</b> (<c>InvoiceLineType.RoomCharge</c>) girer: ekstralar
/// (kahvaltı, minibar) ve Kurtaxe ADR'ye <b>girmez</b>. Hem net (KDV hariç) hem brüt sürüm
/// döner; <c>adrNet</c> birincildir çünkü DE/AT pazarında ADR alışılmış olarak KDV'siz
/// yayımlanır ve KDV otelin geliri değildir.</para>
///
/// <para><b>6) RevPAR</b> = <c>oda geliri / müsait oda-gece</c>. Cebirsel olarak
/// <c>RevPAR = ADR × doluluk</c>'tur ve bu özdeşlik burada <b>tanım gereği</b> sağlanır: her üç
/// metrik de aynı <c>satılan</c> ve <c>müsait</c> sayılarını kullanır. Yine de
/// <c>ReportReader</c> iki yoldan hesaplayıp karşılaştırır ve sapma olursa uyarı loglar
/// (regresyon ağı).</para>
///
/// <para><b>7) Gelirin kaynağı — faturalar.</b> Ciro <b>kesinleşmiş faturalardan</b> okunur
/// (muhasebe gerçeği), rezervasyon tutarından değil. Ayrıntı ve gerekçe:
/// <see cref="RevenueRecognition"/>.</para>
///
/// <para><b>8) Bilinen sınırlar (ürün kararı gerektirir).</b>
/// <list type="bullet">
///   <item><c>Room.IsOutOfOrder</c> <b>tarih aralığı taşımaz</b>; anlık durumdur. Bu yüzden
///   servis dışılık tüm rapor dönemine uygulanır. Tarihsel doğruluk için tarih aralıklı bir
///   <c>RoomBlock</c> kaydı gerekir (şema değişikliği).</item>
///   <item>Odalar soft-delete edilir; bugün silinen bir oda <b>geçmiş</b> kapasiteden de düşer.</item>
///   <item>Konaklama geliri gecelere <b>eşit</b> dağıtılır (bkz. <see cref="RevenueRecognition"/>),
///   çünkü fatura satırı gece gece değil konaklama bazında üretilir.</item>
/// </list></para>
/// </summary>
internal static class ReportDefinitions
{
    /// <summary>Para ve oran alanlarının ondalık hane sayısı (fatura modülüyle aynı).</summary>
    public const int Scale = 2;

    /// <summary>
    /// Rapor aralığının en fazla gün sayısı. Doluluk <i>grid</i>'indeki 92 gün sınırı yanıtın
    /// <c>oda × gün</c> matrisi olmasındandı; rapor yanıtı ise <b>toplamlar + gün başına tek
    /// satır</b> döndürür, yani doğrusal büyür. Bu yüzden sınır müsaitlik ucuyla aynı yere,
    /// bir yıla (366 gün — artık yıl dâhil) çekilmiştir: yıllık bütçe/performans raporu tek
    /// istekte alınabilir, kazara "tüm zamanlar" sorgusu ise <b>400</b> ile reddedilir
    /// (sessizce kırpmak istemciyi yanıltırdı).
    /// </summary>
    public const int MaxRangeDays = 366;
}

/// <summary>
/// <b>Gelirin kaynağı kararı</b> — raporlama modülünün en kritik tanımı.
///
/// <para><b>Karar: ciro FATURALARDAN okunur.</b> İki aday vardı:
/// <list type="number">
///   <item><b>Faturalar</b> (<c>Invoice</c> + <c>InvoiceLineItem</c>): muhasebe gerçeği. GoBD
///   belgesi, KDV beyanı ve gelir tablosu bu kayıtlardan üretilir.</item>
///   <item><b>Rezervasyonlar</b> (<c>Reservation.TotalAmount</c>): operasyonel görünüm; henüz
///   faturalanmamış konaklamaları da içerir, ama muhasebeyle uzlaşmaz (ekstralar, indirimler,
///   iptal bedelleri fatura tarafında oluşur).</item>
/// </list>
/// Ciro raporu <b>muhasebeyle tutarlı olmak zorundadır</b> — "ciro" kelimesi iki farklı sayıyı
/// gösteremez. Bu yüzden birincil kaynak faturalardır. <b>Uyarı:</b> henüz faturalanmamış
/// konaklamalar ciroya <b>girmez</b>. Bu fark gizlenmez: rezervasyon tabanlı görünüm
/// <c>unbilledRoomRevenueGross</c> alanında <b>ayrı</b> olarak döner, böylece tüketici
/// "operasyonel" ile "muhasebe" arasındaki farkı görebilir ve tek bir sayı iki anlamda
/// kullanılmaz.</para>
///
/// <para><b>Hangi faturalar sayılır: <c>IssuedAt != null</c> olanlar.</b> Yani <b>numara almış</b>
/// (bir kez kesinleşmiş) her belge. Sonuçları:
/// <list type="bullet">
///   <item><c>Draft</c> <b>sayılmaz</b> — taslak belge değildir, numarası yoktur, terk edilebilir.</item>
///   <item>Taslakken iptal edilen fatura da sayılmaz (durumu <c>Cancelled</c>'dır ama
///   <c>IssuedAt</c> null'dır). Durum yerine <c>IssuedAt</c>'e bakmanın nedeni tam olarak bu iki
///   <c>Cancelled</c> halini ayırt edebilmektir.</item>
///   <item>Kesinleştikten sonra iptal edilen fatura (<c>Cancelled</c>, <c>IssuedAt</c> dolu)
///   <b>sayılır</b> ve onu iptal eden <b>Stornorechnung</b> da sayılır. Storno satırları
///   orijinalin negatif aynasıdır (fatura modülü kararı) → ikisi toplamda <b>tam sıfır</b> eder.
///   <b>Neden iptal edileni dışlamıyoruz:</b> dışlasaydık storno'nun negatif tutarı tek başına
///   kalır ve rapor <b>hayali negatif ciro</b> gösterirdi. Netleştirme ancak çiftin iki tarafı
///   da sayıldığında doğrudur.</item>
///   <item>Ödeme durumu <b>hiç dikkate alınmaz</b>: ciro tahakkuk esaslıdır (Soll-Versteuerung),
///   tahsilat değildir. <c>Finalized</c> ile <c>Paid</c> arasında ciro farkı yoktur.</item>
/// </list></para>
///
/// <para><b>Döneme atıf (Periodenabgrenzung): konaklama gecelerine eşit dağıtım.</b>
/// Rezervasyona bağlı bir faturanın <c>RoomCharge</c> satırı, konaklamanın <b>tamamı</b> için
/// tek satırdır (<c>quantity = gece sayısı</c>, <c>serviceDate = giriş günü</c>). Belge tarihine
/// (<c>issuedAt</c>) veya Leistungsdatum'a göre atıf yapılsaydı 5 gecelik bir konaklamanın tüm
/// geliri <b>giriş gününe</b> düşer, dönem sınırında duran konaklamalar tümüyle bir tarafa
/// yazılır ve <c>ADR = gelir / oda-gece</c> anlamsızlaşırdı (pay ile payda farklı gecelere ait
/// olurdu). Bu yüzden rezervasyona bağlı gelir <b>konaklamanın gecelerine eşit dağıtılır</b> ve
/// rapor penceresine düşen geceler kadarı sayılır:
/// <c>dönem geliri = toplam gelir × (penceredeki gece) / (toplam gece)</c>.
/// Böylece gelir ve oda-gece <b>aynı gecelere</b> aittir; ADR/RevPAR anlamlıdır ve günlük seri
/// çizilebilir. <b>Bilinen sadeleştirme:</b> gerçek gecelik fiyat sezon içinde değişebilir
/// (rezervasyon modülü gece gece fiyatlar) ama faturaya tek satır olarak yazılır; eşit dağıtım
/// bu satırdan türetilebilecek en iyi yaklaşımdır. Gece bazında kesin atıf istenirse fatura
/// satırlarının gece gece üretilmesi gerekir — <b>ürün kararı</b>.</para>
///
/// <para><b>Dağıtılamayan gelir ayrı gösterilir.</b> Rezervasyona bağlı <b>olmayan</b> faturalar
/// (elle kesilen) ile <c>Cancelled</c>/<c>NoShow</c> rezervasyona bağlı faturalar (iptal bedeli,
/// Ausfallentschädigung) bir konaklama gecesine dağıtılamaz. Bunlar
/// <c>otherInvoicedRevenue</c> altında, satırın <b>Leistungsdatum</b>'una
/// (<c>InvoiceLineItem.ServiceDate</c>) göre raporlanır ve <c>totalRevenue</c>'ya
/// <b>dâhil edilmez</b> — ADR/RevPAR'ı bozmasın diye. Toplam muhasebe cirosu isteniyorsa
/// <c>totalRevenue + otherInvoicedRevenue.total</c> kullanılır.</para>
///
/// <para><b>Kurtaxe (City Tax) gelir DEĞİLDİR.</b> Belediyenin misafirden aldığı, otelin yalnızca
/// tahsil edip aktardığı bir kalemdir (durchlaufender Posten — fatura modülüyle aynı gerekçe).
/// <c>cityTaxCollected</c> alanında ayrı gösterilir; ne <c>totalRevenue</c>'ya ne ADR'ye girer.</para>
///
/// <para><b>Net mi brüt mü:</b> ikisi de döner ve <b>adları açıktır</b> (<c>net</c>, <c>vat</c>,
/// <c>gross</c>). Birincil ciro <b>net</b>tir (KDV devlete aittir, otelin geliri değildir);
/// ADR/RevPAR'ın hem <c>...Net</c> hem <c>...Gross</c> sürümü ayrı alanlarda verilir ki tek bir
/// sayı iki anlamda kullanılmasın.</para>
/// </summary>
internal static class RevenueRecognition
{
    // Bu sınıf yalnızca tanımların yaşadığı yerdir; davranış ReportDataSource/ReportAggregator
    // içindedir ve bu dosyaya atıfla belgelenmiştir.
}

/// <summary>Rapor kapsamı (tek otel / konsolide) — yanıtta okunur biçimde döner.</summary>
public static class ReportScopeModes
{
    /// <summary>Tek otel: <c>X-Hotel-Id</c> ile seçilmiş (veya kullanıcının varsayılan) otel.</summary>
    public const string Hotel = "Hotel";

    /// <summary>
    /// Konsolide: Head Office kullanıcısı aktif otel seçmemiştir; rapor erişilebilir <b>tüm</b>
    /// otelleri kapsar.
    /// </summary>
    public const string Consolidated = "Consolidated";
}
