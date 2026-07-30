using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.TimeEntries.Common;

/// <summary>
/// Zaman kaydı sorgularının paylaşılan parçaları: izdüşüm ve DTO'ya çevrim.
/// Çalışan adı JOIN ile alınır (Include yerine izdüşüm: yalnızca iki kolon okunur).
/// </summary>
internal static class TimeEntryQueryExtensions
{
    public static IQueryable<TimeEntryRow> ProjectToRow(this IQueryable<TimeEntry> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(entry => new TimeEntryRow(
            entry.Id,
            entry.EmployeeId,
            (entry.Employee.FirstName + " " + entry.Employee.LastName) ?? string.Empty,
            entry.ClockIn,
            entry.ClockOut,
            entry.BreakMinutes,
            entry.Source,
            entry.Note));
    }

    public static TimeEntryResponse ToResponse(this TimeEntryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new TimeEntryResponse
        {
            Id = row.Id,
            EmployeeId = row.EmployeeId,
            EmployeeName = row.EmployeeName,
            ClockIn = row.ClockIn,
            ClockOut = row.ClockOut,
            BreakMinutes = row.BreakMinutes,
            WorkedMinutes = TimeEntryRules.CalculateWorkedMinutes(row.ClockIn, row.ClockOut, row.BreakMinutes),
            Source = row.Source.ToString(),
            Note = row.Note,
            IsOpen = row.ClockOut is null,
        };
    }
}
