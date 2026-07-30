using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.ClockOut;

/// <summary>
/// Mesai çıkışı: çalışanın açık kaydını kapatır. Açık kayıt yoksa <b>409</b>
/// (yanlışlıkla yeni bir kayıt uydurulmaz).
/// </summary>
internal sealed class ClockOutHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    EmployeeLookup employees,
    TimeEntryReader reader)
    : IRequestHandler<ClockOutRequest, TimeEntryResponse>
{
    public async Task<TimeEntryResponse> Handle(
        ClockOutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotelId = currentUser.RequireHotelId();

        var employee = await employees.GetInHotelAsync(request.EmployeeId, hotelId, cancellationToken)
            .ConfigureAwait(false);

        var entry = await reader.GetOpenEntryAsync(employee.Id, cancellationToken)
            .ConfigureAwait(false);

        var clockOut = (request.ClockOut ?? clock.UtcNow).ToUniversalTime();
        var breakMinutes = request.BreakMinutes ?? entry.BreakMinutes;

        // Cikis > giris ve mola <= brut sure; ihlal -> 400 (errors sozlugu ile).
        TimeEntryRules.EnsureConsistent(entry.ClockIn, clockOut, breakMinutes);

        entry.ClockOut = clockOut;
        entry.BreakMinutes = breakMinutes;

        // Not gonderilmediyse (null) mevcut not korunur; bos metin gonderilirse temizlenir.
        if (request.Note is not null)
        {
            entry.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        }

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entry.Id, cancellationToken).ConfigureAwait(false);
    }
}
