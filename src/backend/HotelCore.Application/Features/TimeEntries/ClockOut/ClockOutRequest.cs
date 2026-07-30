using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.ClockOut;

/// <summary>
/// <c>POST /api/v1/time-entries/clock-out</c> gövdesi. Kapatılacak kaydın kimliği <b>istekten
/// alınmaz</b>: çalışanın açık kaydı tanım gereği tektir, sunucu onu bulur.
/// </summary>
public sealed record ClockOutRequest : IRequest<TimeEntryResponse>
{
    /// <summary>Aynı otele ait çalışan olmalıdır; aksi hâlde 404.</summary>
    public Guid EmployeeId { get; init; }

    /// <summary>Çıkış anı. Boş bırakılırsa sunucu saati (UTC). Gelecekte olamaz.</summary>
    public DateTimeOffset? ClockOut { get; init; }

    /// <summary>Mola (dakika). Boş bırakılırsa kayıttaki mevcut değer korunur.</summary>
    public int? BreakMinutes { get; init; }

    /// <summary>Not. Boş bırakılırsa kayıttaki mevcut not korunur.</summary>
    public string? Note { get; init; }
}
