namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Rapor aritmetiği tek yerde: yuvarlama ve <b>sıfıra bölünmeyen</b> oran/ortalama hesapları.
/// <para>
/// Yuvarlama fatura modülüyle aynıdır: 2 ondalık, <b>kaufmännisch</b>
/// (<see cref="MidpointRounding.AwayFromZero"/>). Yuvarlama <b>yalnızca yanıt üretilirken</b>
/// yapılır; ara toplamlar tam <c>decimal</c> hassasiyetinde biriktirilir ki gün gün yuvarlama
/// hatası toplamlara sızmasın.
/// </para>
/// <para>
/// <b>Payda sıfırsa 0 döner</b> (istisna fırlatılmaz, <c>null</c> dönülmez): "hiç oda yok" veya
/// "hiç gece satılmadı" durumunda ADR/RevPAR/doluluk tanımsızdır; grafik çizen istemci için
/// 0 en az sürprizli değerdir ve alanın tipi hep sayı kalır.
/// </para>
/// </summary>
internal static class ReportMath
{
    public static decimal Round(decimal value) =>
        Math.Round(value, ReportDefinitions.Scale, MidpointRounding.AwayFromZero);

    /// <summary>Ortalama: <c>toplam / adet</c> (ADR, RevPAR). Adet 0 ise 0.</summary>
    public static decimal PerUnit(decimal total, int units) =>
        units <= 0 ? 0m : Round(total / units);

    /// <summary>Yüzde: <c>parça / bütün × 100</c>. Bütün 0 ise 0.</summary>
    public static decimal Percent(int part, int whole) =>
        whole <= 0 ? 0m : Round(part * 100m / whole);

    /// <summary>Yüzde (para): payda 0 ise 0. Negatif pay (storno ağırlıklı dönem) korunur.</summary>
    public static decimal Share(decimal part, decimal whole) =>
        whole == 0m ? 0m : Round(part * 100m / whole);
}
