namespace HotelCore.Application.Features.Vacations.Common;

/// <summary>İzin gün sayısı hesabı — tek kaynak (talep, onay ve iptal aynı sayıyı kullanır).</summary>
public static class VacationDays
{
    /// <summary>Bir talebin kapsayabileceği en fazla gün (bir yıl + artık gün).</summary>
    public const int MaxDaysPerRequest = 366;

    /// <summary>
    /// Talep gün sayısı = <b>takvim günü</b>, bitiş tarihi dahil (<c>To − From + 1</c>).
    /// <para>
    /// <b>Bu fazın bilinçli sınırı:</b> hafta sonu ve resmî tatil düşülmez. Almanya'da resmî
    /// tatiller eyalet (Bundesland) bazında değişir ve otelde hafta sonu da normal iş günüdür;
    /// doğru "iş günü" hesabı için otelin çalışma takvimi + eyalet tatil listesi gerekir. Bu
    /// veriler henüz modelde yoktur, bu yüzden sayı takvim günü olarak hesaplanır ve bakiye de
    /// aynı birimden düşülür (tutarlılık korunur). İş günü hesabı geldiğinde <b>yalnızca bu
    /// metot</b> değişir; çağrı yerleri etkilenmez.
    /// </para>
    /// </summary>
    /// <param name="from">İzin başlangıcı.</param>
    /// <param name="to">İzin bitişi (dahil).</param>
    /// <returns>Takvim günü sayısı; <paramref name="to"/> başlangıçtan önceyse 0.</returns>
    public static decimal Calculate(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;

        return days > 0 ? days : 0m;
    }
}
