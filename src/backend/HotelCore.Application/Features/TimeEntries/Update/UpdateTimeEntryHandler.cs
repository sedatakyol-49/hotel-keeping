using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.Update;

/// <summary>
/// Zaman kaydının manuel düzeltmesi. Kayıt yeniden "açık" hâle getirilirse (çıkış null) aynı
/// çalışanın başka açık kaydı olmamalıdır — clock-in ile aynı tekillik kuralı korunur.
/// </summary>
internal sealed class UpdateTimeEntryHandler(IAppDbContext database, TimeEntryReader reader)
    : IRequestHandler<UpdateTimeEntryRequest, TimeEntryResponse>
{
    public async Task<TimeEntryResponse> Handle(
        UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        var clockIn = request.ClockIn.ToUniversalTime();
        var clockOut = request.ClockOut?.ToUniversalTime();

        if (clockOut is null)
        {
            await reader.EnsureNoOpenEntryAsync(entry.EmployeeId, entry.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        TimeEntryRules.EnsureConsistent(clockIn, clockOut, request.BreakMinutes);

        entry.ClockIn = clockIn;
        entry.ClockOut = clockOut;
        entry.BreakMinutes = request.BreakMinutes;
        entry.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entry.Id, cancellationToken).ConfigureAwait(false);
    }
}
