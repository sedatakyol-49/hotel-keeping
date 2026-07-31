namespace HotelCore.Domain.Enums;

/// <summary>Rezervasyon kanalı — ciro raporlarında kanal dağılımı ve OTA komisyonu için.</summary>
public enum ReservationChannel
{
    Direct = 0,
    Phone = 1,
    WalkIn = 2,
    BookingCom = 3,
    Hrs = 4,
    Expedia = 5,
    Corporate = 6,

    /// <summary>
    /// Otelin kendi misafir sitesinden (public rezervasyon kanalı) gelen satış.
    /// <para>
    /// <b>Neden <see cref="Direct"/> değil:</b> web satışını "doğrudan" ile birleştirmek kanal
    /// dağılımı raporunu ve komisyon analizini anlamsız kılar — komisyonsuz web satışı ile
    /// telefon/e-posta satışı farklı maliyet ve farklı pazarlama kararı üretir.
    /// </para>
    /// <para>
    /// <b>Yan etki (architecture-public-booking.md §7.1):</b> fiyat seçimi kanalı <b>birebir</b>
    /// karşılaştırır; bu yüzden <c>Channel = Direct</c> olan mevcut fiyat planları web
    /// rezervasyonlarına <b>uygulanmaz</b>. Web için ya <c>Website</c> planı ya da "tüm kanallar"
    /// (<c>Channel = null</c>) planı bulunmalıdır, yoksa fiyat <c>RoomType.BasePrice</c>'a düşer.
    /// </para>
    /// <para>
    /// Enum <b>metin</b> olarak saklandığı için yeni değer veri migration'ı gerektirmez.
    /// </para>
    /// </summary>
    Website = 7
}
