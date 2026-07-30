namespace HotelCore.Application.Features.TimeEntries.Common;

/// <summary>
/// Zaman kaydı (Zeiterfassung) — api-contracts.md → "HR (Vacation / TimeTracking / Shifts)".
/// </summary>
public sealed record TimeEntryResponse
{
    public Guid Id { get; init; }

    public Guid EmployeeId { get; init; }

    public string EmployeeName { get; init; } = string.Empty;

    /// <summary>Giriş anı (UTC).</summary>
    public DateTimeOffset ClockIn { get; init; }

    /// <summary>Çıkış anı (UTC); mesai sürüyorsa null.</summary>
    public DateTimeOffset? ClockOut { get; init; }

    public int BreakMinutes { get; init; }

    /// <summary>
    /// Mola düşülmüş çalışma süresi (dakika). <b>Sunucuda</b> hesaplanır ki süre tanımı
    /// istemciler arasında farklılaşmasın. Kayıt açıkken (<c>clockOut = null</c>) null döner:
    /// mesai bitmeden süresi belli değildir ve "şu ana kadar" değeri her istekte değişip
    /// yanıtı önbelleklenemez/karşılaştırılamaz hâle getirirdi.
    /// </summary>
    public int? WorkedMinutes { get; init; }

    /// <summary>Kaynak enum <b>adı</b> (string): bu fazda daima <c>Manual</c>.</summary>
    public string Source { get; init; } = string.Empty;

    public string? Note { get; init; }

    /// <summary>Mesai sürüyor mu (<c>clockOut == null</c>) — istemcinin türetmesi gerekmez.</summary>
    public bool IsOpen { get; init; }
}
