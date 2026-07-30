using System.Globalization;

namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>
/// <c>?week=YYYY-Www</c> parametresinin çözümü (ISO 8601 hafta tarihi, örn. <c>2026-W31</c>).
/// <para>
/// <b>Neden ISO 8601:</b> "hafta" tanımı kültüre göre değişir (haftanın ilk günü, yılın ilk
/// haftası). Vardiya planı Almanya'da kullanıldığı için hafta <b>Pazartesi</b> başlar ve yılın
/// ilk haftası 4 Ocak'ı içeren haftadır — bu tam olarak ISO 8601'dir ve .NET'te
/// <see cref="ISOWeek"/> ile kültürden bağımsız hesaplanır. Böylece sunucunun/istemcinin
/// culture ayarı planı kaydırmaz.
/// </para>
/// <para>
/// <b>Aralık:</b> Pazartesi (dahil) – Pazar (dahil), yani 7 gün.
/// </para>
/// </summary>
public static class ShiftWeek
{
    /// <summary>Sözleşmedeki biçimin uzunluğu: <c>YYYY-Www</c>.</summary>
    private const int PatternLength = 8;

    /// <summary>
    /// <c>YYYY-Www</c> etiketini hafta aralığına çevirir. Biçim veya hafta numarası geçersizse
    /// (örn. 53 haftası olmayan bir yılda <c>W53</c>) <c>false</c> döner — çağıran bunu 400'e çevirir.
    /// </summary>
    /// <param name="value">Hafta etiketi (örn. <c>2026-W31</c>).</param>
    /// <param name="monday">Haftanın Pazartesi'si.</param>
    /// <param name="sunday">Haftanın Pazar'ı.</param>
    /// <returns>Etiket geçerliyse <c>true</c>.</returns>
    public static bool TryParse(string? value, out DateOnly monday, out DateOnly sunday)
    {
        monday = default;
        sunday = default;

        if (value is null || value.Length != PatternLength || value[4] != '-')
        {
            return false;
        }

        if (value[5] is not ('W' or 'w'))
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var week))
        {
            return false;
        }

        if (year < 1 || year > 9999 || week < 1 || week > ISOWeek.GetWeeksInYear(year))
        {
            return false;
        }

        monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
        sunday = monday.AddDays(6);

        return true;
    }

    /// <summary>Bir günün ISO hafta etiketi (<c>YYYY-Www</c>) — yanıtta geri döndürülür.</summary>
    public static string Label(DateOnly date)
    {
        var reference = date.ToDateTime(TimeOnly.MinValue);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ISOWeek.GetYear(reference):D4}-W{ISOWeek.GetWeekOfYear(reference):D2}");
    }
}
