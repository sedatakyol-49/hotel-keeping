using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Application.Features.TimeEntries.Common;

/// <summary>
/// Zaman kaydının zaman/mola kuralları — tek kaynak. Kurallar hem gövdeden hem veritabanındaki
/// mevcut değerlerden beslendiği için (clock-out yalnızca çıkış saatini gönderir, giriş saati
/// kayıttan okunur) kısmen validator'a taşınamaz; ihlal <b>400</b> (<c>errors</c> sözlüğü ile) döner.
/// </summary>
public static class TimeEntryRules
{
    /// <summary>Mola en fazla bir gün (dakika).</summary>
    public const int MaxBreakMinutes = 1440;

    /// <summary>
    /// İstemci saatinin sunucudan ileri olabileceği tolerans. Bu pay olmadan doğru çalışan bir
    /// istemci, birkaç saniyelik saat kayması yüzünden "gelecek tarihli kayıt" hatası alırdı.
    /// </summary>
    public const int ClockSkewToleranceMinutes = 2;

    /// <summary>Verilen an (varsa) sunucu saatinin tolerans payı içinde mi?</summary>
    public static bool IsNotInFuture(DateTimeOffset? value, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return value is null || value <= clock.UtcNow.AddMinutes(ClockSkewToleranceMinutes);
    }

    /// <summary>
    /// Mola düşülmüş çalışma süresi (dakika); kayıt açıkken null. Saniyeler aşağıya yuvarlanır
    /// (tamamlanmamış dakika çalışılmış saymaz) ve sonuç negatife düşmez.
    /// </summary>
    public static int? CalculateWorkedMinutes(
        DateTimeOffset clockIn,
        DateTimeOffset? clockOut,
        int breakMinutes)
    {
        if (clockOut is not DateTimeOffset end)
        {
            return null;
        }

        var grossMinutes = (int)Math.Floor((end - clockIn).TotalMinutes);

        return Math.Max(0, grossMinutes - breakMinutes);
    }

    /// <summary>
    /// Çıkış girişten sonra olmalı ve mola brüt süreyi aşmamalıdır. Aksi hâlde
    /// <see cref="ValidationException"/> (400) — alan adları PascalCase'dir (sözleşme kuralı).
    /// </summary>
    public static void EnsureConsistent(DateTimeOffset clockIn, DateTimeOffset? clockOut, int breakMinutes)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (breakMinutes is < 0 or > MaxBreakMinutes)
        {
            errors["BreakMinutes"] = [$"0 ile {MaxBreakMinutes} arasinda olmalidir."];
        }

        if (clockOut is DateTimeOffset end)
        {
            if (end <= clockIn)
            {
                errors["ClockOut"] = ["Cikis saati giris saatinden sonra olmalidir."];
            }
            else if (!errors.ContainsKey("BreakMinutes"))
            {
                var grossMinutes = (int)Math.Floor((end - clockIn).TotalMinutes);
                if (breakMinutes > grossMinutes)
                {
                    errors["BreakMinutes"] =
                        [$"Mola suresi calisma suresini ({grossMinutes} dk) asamaz."];
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
