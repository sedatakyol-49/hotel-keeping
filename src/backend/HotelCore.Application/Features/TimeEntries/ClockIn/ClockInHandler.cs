using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.TimeEntries.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.TimeEntries.ClockIn;

/// <summary>
/// Mesai girişi. Aynı çalışanın açık (çıkışı yapılmamış) kaydı varken ikinci giriş <b>409</b>
/// döner: aksi hâlde hangi kaydın kapatılacağı belirsizleşir ve süre iki kez sayılırdı.
/// </summary>
internal sealed class ClockInHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    EmployeeLookup employees,
    TimeEntryReader reader)
    : IRequestHandler<ClockInRequest, TimeEntryResponse>
{
    public async Task<TimeEntryResponse> Handle(
        ClockInRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();

        var employee = await employees.GetInHotelAsync(request.EmployeeId, hotelId, cancellationToken)
            .ConfigureAwait(false);

        await reader.EnsureNoOpenEntryAsync(employee.Id, excludeId: null, cancellationToken)
            .ConfigureAwait(false);

        // Npgsql timestamptz'a yalnizca offset'i 0 olan degeri yazar; gelen deger UTC'ye cevrilir.
        var clockIn = (request.ClockIn ?? clock.UtcNow).ToUniversalTime();

        var entry = new TimeEntry
        {
            HotelId = hotelId,
            EmployeeId = employee.Id,
            ClockIn = clockIn,
            ClockOut = null,
            BreakMinutes = 0,
            Source = TimeEntrySource.Manual,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };

        database.TimeEntries.Add(entry);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entry.Id, cancellationToken).ConfigureAwait(false);
    }
}
