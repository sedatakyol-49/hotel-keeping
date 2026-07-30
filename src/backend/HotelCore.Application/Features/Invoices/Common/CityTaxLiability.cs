using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// <b>"Bu rezervasyon için Kurtaxe doğdu mu?"</b> sorusunun <b>tek</b> tanımı.
///
/// <para><b>Hukuki dayanak.</b> Kurtaxe / Kurbeitrag / Übernachtungsteuer federal bir vergi
/// değildir: belediye tüzükleriyle (<i>Kurbeitragssatzung</i>, <i>Satzung über die Erhebung einer
/// Kurtaxe</i>) konur ve tüzüklerin ortak vergiyi doğuran olayı (<i>Steuertatbestand</i>)
/// <b>fiilen gerçekleşen konaklamadır</b> (<i>Übernachtung</i>). Vergi kişi ve <b>geçirilen gece</b>
/// başına doğar. Konaklama hiç gerçekleşmediyse vergiyi doğuran olay da yoktur; otel bu durumda
/// belediye adına tahsil edeceği (ve belediyeye aktaracağı) bir tutarı misafirden <b>isteyemez</b>.
/// Otelin buradaki rolü tahsil aracısıdır — tutar otelin bedelinin (<i>Entgelt</i>) parçası değil,
/// <i>durchlaufender Posten</i>'dir (UStG §10 Abs. 1 Satz 5; bkz.
/// <see cref="InvoiceAmounts"/> §3).</para>
///
/// <para><b>Sonuç:</b> <see cref="ReservationStatus.NoShow"/> ve
/// <see cref="ReservationStatus.Cancelled"/> durumundaki rezervasyondan üretilen faturada
/// <b>Kurtaxe satırı hiç oluşturulmaz</b> — fiilen konaklanan gece sayısı sıfırdır, dolayısıyla
/// matrah da sıfırdır. Sıfır tutarlı bir kalem yazmak yerine satır hiç üretilmez (mevcut
/// "vergiye tabi kişi kalmadıysa satır yazma" kuralıyla aynı davranış).</para>
///
/// <para><b>No-show ücretinin kendisi ayrı bir konudur:</b> misafirden alınan no-show/iptal bedeli
/// bir <i>tazminattır</i> (echter Schadensersatz), konaklama bedeli değildir. Bu belge o satıra
/// dokunmaz; KDV tartışması için bkz. README "Canlıya çıkmadan mali onay isteyen kararlar".</para>
///
/// <para><b>Kapsam dışı bırakılanlar (bilinçli):</b>
/// <list type="bullet">
///   <item><see cref="ReservationStatus.Option"/> konaklamanın <i>gerçekleşmeyeceğini</i>
///   söylemez (henüz kesinleşmemiş bir taleptir); davranışı değiştirilmez.</item>
///   <item><b>Erken çıkış</b> (planlanan 3 gece, fiilen 2 gece) burada ele alınmaz: gece sayısı
///   hâlâ rezervasyonun <c>CheckIn</c>/<c>CheckOut</c> aralığından gelir. Fiilî gece sayısını
///   güvenilir biçimde türetmek için otel yerel takvim gününe göre <b>fiilî</b> giriş/çıkış
///   tarihi gerekir; <c>Reservation.CheckedInAt/CheckedOutAt</c> UTC <i>an</i> damgasıdır ve
///   <c>Hotel</c>'de saat dilimi kolonu yoktur — gün sınırında bir gece kayarak yanlış Kurtaxe
///   beyanı üretebilir. Şema ihtiyacı olarak raporlanmıştır; <b>mali müşavir + belediye tüzüğü
///   onayı gerekir</b>.</item>
/// </list></para>
/// </summary>
internal static class CityTaxLiability
{
    /// <summary>
    /// Verilen rezervasyon durumunda Kurtaxe'yi doğuran olay (fiilî konaklama) gerçekleşmiş
    /// <b>olabilir</b> mi. <c>false</c> ise fatura Kurtaxe satırı taşımaz.
    /// </summary>
    public static bool ArisesFrom(ReservationStatus status) =>
        status is not (ReservationStatus.NoShow or ReservationStatus.Cancelled);
}
