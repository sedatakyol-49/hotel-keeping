namespace HotelCore.Application.Features.Availability.Common;

/// <summary>
/// Tarih aralığı sınırları — istemcinin kazara çok büyük bir matris istemesini engeller.
/// Sınır aşılırsa <b>400</b> döner (sessizce kırpmak istemciyi yanıltırdı: eksik veriyi tam
/// sanardı).
/// </summary>
public static class AvailabilityLimits
{
    /// <summary>
    /// Doluluk grid'inde en fazla gün sayısı (≈ 3 ay). Yanıt <c>oda × gün</c> matrisi olduğu için
    /// büyüklük çarpımsaldır: 100 oda × 92 gün ≈ 9.200 hücre üst sınırı. Yıllık matris (365 gün)
    /// hem sunucuda hem tarayıcıda anlamsız yük demektir; takvim ekranı zaten aylık/haftalık
    /// pencerelerle çalışır.
    /// </summary>
    public const int MaxOccupancyRangeDays = 92;

    /// <summary>
    /// Müsaitlik sorgusunda en fazla gün sayısı. Yanıt oda başına tek satır olduğu için grid'e
    /// göre daha gevşektir; yine de bir yıl ile sınırlanır (uzun vadeli kontrat talepleri için yeterli).
    /// </summary>
    public const int MaxAvailabilityRangeDays = 366;
}
