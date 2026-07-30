using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Rapor kapsamındaki bir otelin künyesi (kırılım başlıkları ve para birimi kontrolü için).
/// </summary>
/// <param name="Id">Otel kimliği.</param>
/// <param name="Name">Otel adı.</param>
/// <param name="Currency">ISO 4217 para birimi.</param>
internal sealed record ReportHotelInfo(Guid Id, string Name, string Currency);

/// <summary>
/// Otel bazında oda kapasitesi — <b>SQL'de</b> <c>GROUP BY HotelId</c> ile hesaplanır
/// (satırlar belleğe çekilmez).
/// </summary>
/// <param name="HotelId">Otel kimliği.</param>
/// <param name="RoomCount">Silinmemiş oda sayısı (global soft-delete filtresi uygulanmış).</param>
/// <param name="OutOfOrderRoomCount">Servis dışı oda sayısı.</param>
internal sealed record RoomCapacityRow(Guid HotelId, int RoomCount, int OutOfOrderRoomCount);

/// <summary>
/// Konaklama kovası: aynı <c>(otel, giriş, çıkış, kanal)</c> dörtlüsüne sahip rezervasyonların
/// <b>SQL'de</b> toplanmış hâli.
/// <para>
/// <b>Neden bu grup anahtarı:</b> gece sayısı yalnızca <c>(CheckIn, CheckOut)</c>'a bağlıdır;
/// aynı gruptaki tüm rezervasyonlar aynı gecelere düştüğü için gruba ait toplam tutar tek bir
/// bölme ile gecelere dağıtılabilir. Böylece hem toplamlar SQL'de hesaplanır hem de günlük seri
/// için gereken gün bilgisi (giriş/çıkış) korunur — satır satır rezervasyon çekmeye gerek kalmaz.
/// Grup sayısı hiçbir zaman rezervasyon sayısını aşamaz, tipik olarak çok daha azdır.
/// </para>
/// </summary>
/// <param name="HotelId">Otel kimliği.</param>
/// <param name="CheckIn">Giriş günü (dâhil).</param>
/// <param name="CheckOut">Çıkış günü (dâhil değil).</param>
/// <param name="Channel">Rezervasyon kanalı.</param>
/// <param name="ReservationCount">Gruptaki rezervasyon sayısı.</param>
/// <param name="ReservationAmount">Gruptaki <c>Reservation.TotalAmount</c> toplamı (brüt).</param>
internal sealed record StayGroupRow(
    Guid HotelId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    ReservationChannel Channel,
    int ReservationCount,
    decimal ReservationAmount);

/// <summary>
/// Konaklamaya bağlı <b>kesinleşmiş</b> fatura satırlarının, konaklama kovası ve satır türü
/// bazında <b>SQL'de</b> toplanmış hâli.
/// </summary>
/// <param name="HotelId">Otel kimliği (rezervasyondan).</param>
/// <param name="CheckIn">Konaklamanın giriş günü.</param>
/// <param name="CheckOut">Konaklamanın çıkış günü.</param>
/// <param name="Channel">Rezervasyon kanalı.</param>
/// <param name="Type">Fatura satırı türü (<c>RoomCharge</c> / <c>Extra</c> / <c>CityTax</c>).</param>
/// <param name="Net">KDV hariç toplam.</param>
/// <param name="Vat">KDV toplamı.</param>
internal sealed record StayMoneyRow(
    Guid HotelId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    ReservationChannel Channel,
    InvoiceLineType Type,
    decimal Net,
    decimal Vat);

/// <summary>
/// Konaklama gecelerine <b>dağıtılamayan</b> kesinleşmiş fatura satırları: rezervasyona bağlı
/// olmayan (elle kesilmiş) faturalar ve <c>Cancelled</c>/<c>NoShow</c> rezervasyona bağlı
/// faturalar (iptal bedeli). Dönem ataması satırın <b>Leistungsdatum</b>'una göredir.
/// </summary>
/// <param name="HotelId">Otel kimliği (satırın tenant kolonu).</param>
/// <param name="Type">Fatura satırı türü.</param>
/// <param name="Net">KDV hariç toplam.</param>
/// <param name="Vat">KDV toplamı.</param>
internal sealed record OtherInvoiceRow(Guid HotelId, InvoiceLineType Type, decimal Net, decimal Vat);
