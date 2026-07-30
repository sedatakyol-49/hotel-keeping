using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.ClockIn;

/// <summary>
/// <c>POST /api/v1/time-entries/clock-in</c> gövdesi. Kaynak sunucuda <c>Manual</c> olarak
/// damgalanır (istekten okunmaz).
/// </summary>
public sealed record ClockInRequest : IRequest<TimeEntryResponse>
{
    /// <summary>Aynı otele ait çalışan olmalıdır; aksi hâlde 404.</summary>
    public Guid EmployeeId { get; init; }

    /// <summary>
    /// Giriş anı. Boş bırakılırsa sunucu saati (UTC) kullanılır — normal kullanım budur.
    /// Verilirse gelecekte olamaz (küçük saat kayması payı hariç).
    /// </summary>
    public DateTimeOffset? ClockIn { get; init; }

    public string? Note { get; init; }
}
