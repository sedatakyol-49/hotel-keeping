namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Rapor dönemi: <b>kapalı</b> gün aralığı <c>[From, To]</c> ve ondan türetilen <b>yarı açık</b>
/// gece penceresi <c>[From, NightEndExclusive)</c>.
/// <para>
/// İki gösterimin bir arada tutulması bilinçlidir: istemci gün konuşur ("1–7 Ağustos"),
/// rezervasyon modeli gece konuşur (<c>[checkIn, checkOut)</c>). Dönüşüm <b>tek yerde</b>
/// yapılır ki "çıkış günü gece saymaz" kuralı raporda da birebir korunsun.
/// </para>
/// </summary>
internal sealed class ReportPeriod
{
    public ReportPeriod(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
        DayCount = to.DayNumber - from.DayNumber + 1;
        NightEndExclusive = to.AddDays(1);
    }

    /// <summary>Aralığın ilk günü (dâhil).</summary>
    public DateOnly From { get; }

    /// <summary>Aralığın son günü (<b>dâhil</b>).</summary>
    public DateOnly To { get; }

    /// <summary>
    /// Gece penceresinin üst sınırı (<b>dâhil değil</b>) = <c>To + 1 gün</c>. Rezervasyon
    /// kesişim testleri bu değerle yapılır (<c>AvailabilityQuery.BlockingBetween</c>).
    /// </summary>
    public DateOnly NightEndExclusive { get; }

    /// <summary>Aralıktaki gün (= gece) sayısı; <c>To == From</c> ise 1.</summary>
    public int DayCount { get; }

    /// <summary>Bir günün seri dizisindeki indeksi. Aralık dışı gün için negatif/taşkın döner.</summary>
    public int IndexOf(DateOnly date) => date.DayNumber - From.DayNumber;

    /// <summary>Verilen günün <c>i</c>. indeksteki karşılığı.</summary>
    public DateOnly DateAt(int index) => From.AddDays(index);

    /// <summary>
    /// Bir konaklamanın rapor penceresine düşen gece aralığını kırpar.
    /// Kesişim yoksa <c>Length == 0</c> döner.
    /// </summary>
    public ClippedNights Clip(DateOnly checkIn, DateOnly checkOut)
    {
        var start = checkIn > From ? checkIn : From;
        var endExclusive = checkOut < NightEndExclusive ? checkOut : NightEndExclusive;
        var length = endExclusive.DayNumber - start.DayNumber;

        return new ClippedNights(
            start.DayNumber - From.DayNumber,
            length > 0 ? length : 0);
    }
}

/// <summary>Konaklamanın rapor penceresine düşen kısmı (dizi indeksi + gece sayısı).</summary>
/// <param name="StartIndex">Günlük seri dizisindeki ilk indeks.</param>
/// <param name="Length">Penceredeki gece sayısı (0 = kesişim yok).</param>
internal readonly record struct ClippedNights(int StartIndex, int Length);
