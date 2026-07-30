using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>
/// Vardiya yanıtlarının tek üretim noktası (plan ızgarası + tek kayıt) ve tekillik kontrolü.
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class ShiftReader(IAppDbContext database)
{
    /// <summary>
    /// Gün × çalışan plan ızgarası.
    /// <para>
    /// Filtreleme ve sıralama SQL'de yapılır; günlere dağıtım zaten okunmuş satırlar üzerinde
    /// tek geçişte hesaplanır (aralık en fazla <see cref="ShiftPlanRange.MaxRangeDays"/> gün
    /// olduğu için bu bellekte ucuzdur ve gün başına ayrı sorgu atılmaz).
    /// </para>
    /// </summary>
    public async Task<ShiftPlanResponse> GetPlanAsync(
        ShiftPlanRange range,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);

        var shifts = await database.Shifts
            .Where(shift => shift.Date >= range.From && shift.Date <= range.To)
            .OrderBy(shift => shift.Date)
            .ThenBy(shift => shift.Employee.LastName)
            .ThenBy(shift => shift.Employee.FirstName)
            .ThenBy(shift => shift.Id)
            .Project()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Satir ekseni: aralik baslangicindan once ayrilmis calisanlar plana alinmaz.
        var employees = await database.Employees
            .Where(employee => employee.TerminatedOn == null || employee.TerminatedOn >= range.From)
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ThenBy(employee => employee.Id)
            .Select(employee => new ShiftPlanEmployeeDto(
                employee.Id,
                employee.FirstName + " " + employee.LastName,
                employee.Department.Name ?? string.Empty))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var days = new List<ShiftPlanDayDto>();
        for (var date = range.From; date <= range.To; date = date.AddDays(1))
        {
            days.Add(new ShiftPlanDayDto(date, shifts.FindAll(shift => shift.Date == date)));
        }

        return new ShiftPlanResponse
        {
            From = range.From,
            To = range.To,
            Week = range.Week,
            Days = days,
            Employees = employees,
        };
    }

    /// <summary>Tek vardiya; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<ShiftResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var shift = await database.Shifts
            .Where(candidate => candidate.Id == id)
            .Project()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return shift ?? throw new NotFoundException(nameof(Shift), id);
    }

    public async Task<Shift> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var shift = await database.Shifts
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return shift ?? throw new NotFoundException(nameof(Shift), id);
    }

    /// <summary>
    /// Bir çalışana aynı gün için tek vardiya planlanır. Ön kontrol anlamlı mesaj verir; yarış
    /// durumunda <c>(EmployeeId, Date)</c> unique index'i devreye girer ve ihlal Infrastructure'da
    /// 409'a çevrilir (bkz. <c>AppDbContext</c>).
    /// </summary>
    public async Task EnsureDateIsFreeAsync(
        Guid employeeId,
        DateOnly date,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await database.Shifts
            .AnyAsync(
                shift => shift.EmployeeId == employeeId
                         && shift.Date == date
                         && (excludeId == null || shift.Id != excludeId),
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new ConflictException(Messages.ShiftAlreadyExists(date));
        }
    }
}

/// <summary>
/// Vardiya izdüşümü — çalışan adı JOIN ile alınır (Include yerine izdüşüm). Çalışan
/// soft-delete edilmişse JOIN boş döner ve ad boş metne düşülür.
/// </summary>
internal static class ShiftQueryExtensions
{
    public static IQueryable<ShiftResponse> Project(this IQueryable<Shift> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(shift => new ShiftResponse
        {
            Id = shift.Id,
            EmployeeId = shift.EmployeeId,
            EmployeeName = (shift.Employee.FirstName + " " + shift.Employee.LastName) ?? string.Empty,
            Date = shift.Date,
            ShiftType = shift.ShiftType.ToString(),
            Note = shift.Note,
        });
    }
}
