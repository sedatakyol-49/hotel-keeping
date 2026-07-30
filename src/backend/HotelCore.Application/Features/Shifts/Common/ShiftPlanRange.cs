namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>
/// Vardiya planının çözümlenmiş tarih aralığı.
/// </summary>
/// <param name="From">Aralık başlangıcı (dahil).</param>
/// <param name="To">Aralık bitişi (dahil).</param>
/// <param name="Week">
/// İstek <c>week</c> ile geldiyse ISO hafta etiketi; serbest <c>from/to</c> aralığında null.
/// Yanıtta geri döner ki istemci hangi haftayı gördüğünü doğrulayabilsin.
/// </param>
internal sealed record ShiftPlanRange(DateOnly From, DateOnly To, string? Week)
{
    /// <summary>Bir istekte istenebilecek en uzun aralık (yaklaşık iki ay).</summary>
    public const int MaxRangeDays = 62;

    /// <summary>
    /// Öncelik sırası (sözleşmede de belirtilir):
    /// <list type="number">
    ///   <item><c>week</c> verilmişse <b>o kazanır</b> — <c>from/to</c> yok sayılır. Gerekçe:
    ///         <c>week</c> tek başına kesin bir aralık tanımlar; ikisi çelişirse sunucunun
    ///         sessizce birini seçmesi yerine <b>daha spesifik olan</b> adlandırılmış dönem
    ///         uygulanır ve yanıttaki <c>week</c>/<c>from</c>/<c>to</c> alanları hangi aralığın
    ///         kullanıldığını açıkça bildirir.</item>
    ///   <item>Yalnızca <c>from</c> + <c>to</c> verilmişse o aralık kullanılır (ikisi birlikte
    ///         zorunludur — validator tekini reddeder).</item>
    ///   <item>Hiçbiri verilmemişse sunucu saatinin (UTC) içinde bulunduğu ISO hafta.</item>
    /// </list>
    /// Biçim/aralık doğrulaması validator'da yapılır; burada yalnızca çözüm vardır.
    /// </summary>
    public static ShiftPlanRange Resolve(string? week, DateOnly? from, DateOnly? to, DateOnly today)
    {
        if (!string.IsNullOrWhiteSpace(week) && ShiftWeek.TryParse(week, out var monday, out var sunday))
        {
            return new ShiftPlanRange(monday, sunday, ShiftWeek.Label(monday));
        }

        if (from is DateOnly start && to is DateOnly end)
        {
            return new ShiftPlanRange(start, end, null);
        }

        return FromWeekOf(today);
    }

    /// <summary>Verilen günün içinde bulunduğu ISO hafta (Pazartesi – Pazar).</summary>
    private static ShiftPlanRange FromWeekOf(DateOnly date)
    {
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        return new ShiftPlanRange(monday, monday.AddDays(6), ShiftWeek.Label(monday));
    }
}
