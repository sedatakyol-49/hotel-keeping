using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Vacations.Common;

/// <summary>
/// İzin yanıtlarının tek üretim noktası + slice'ların paylaştığı iş kuralı kontrolleri
/// (çakışma, karar verilebilirlik, bakiye satırı).
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class VacationReader(IAppDbContext database)
{
    /// <summary>Bekleyen veya onaylı talepler yeni bir talebin tarihleriyle çakışamaz.</summary>
    private static readonly VacationStatus[] BlockingStatuses =
    [
        VacationStatus.Pending,
        VacationStatus.Approved
    ];

    public async Task<PagedResult<VacationRequestResponse>> ListAsync(
        VacationListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplyFilters(database.VacationRequests, query);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await filtered
            // En yeni talep en ustte; esitlikte Id ile kararli siralama (sayfalama kaymasin).
            .OrderByDescending(request => request.From)
            .ThenByDescending(request => request.CreatedAt)
            .ThenBy(request => request.Id)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .Project()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<VacationRequestResponse>(
            items,
            query.Paging.Page,
            query.Paging.PageSize,
            totalCount);
    }

    /// <summary>Tek talep; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<VacationRequestResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var vacation = await database.VacationRequests
            .Where(candidate => candidate.Id == id)
            .Project()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return vacation ?? throw new NotFoundException(nameof(VacationRequest), id);
    }

    public async Task<VacationRequest> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var vacation = await database.VacationRequests
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return vacation ?? throw new NotFoundException(nameof(VacationRequest), id);
    }

    /// <summary>
    /// Karar (onay/ret) için talebi getirir. Yalnızca <see cref="VacationStatus.Pending"/> talep
    /// karara bağlanabilir; zaten karara bağlanmış talebe ikinci karar <b>409</b> döner — böylece
    /// bakiye iki kez artmaz.
    /// </summary>
    public async Task<VacationRequest> GetPendingTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var vacation = await GetTrackedAsync(id, cancellationToken).ConfigureAwait(false);

        if (vacation.Status is not VacationStatus.Pending)
        {
            throw new ConflictException(
                $"Bu izin talebi zaten karara baglandi (durum: {vacation.Status}); tekrar karar verilemez.");
        }

        return vacation;
    }

    /// <summary>
    /// Aynı çalışan için tarih aralığı çakışan bekleyen/onaylı talep varsa <b>409</b>.
    /// <para>
    /// Kesişim testi kapalı aralıklar üzerinde <c>mevcut.From &lt;= yeni.To &amp;&amp;
    /// mevcut.To &gt;= yeni.From</c> şeklindedir: tek günlük ve uç uca değen aralıklar dâhil tüm
    /// örtüşmeleri yakalar, sadece dokunmayan (bitişik) günler serbest kalır. Sorgu tamamen
    /// SQL'de çalışır ve <c>(EmployeeId, From, To)</c> index'ini kullanır.
    /// </para>
    /// </summary>
    public async Task EnsureNoOverlapAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var overlaps = await database.VacationRequests
            .AnyAsync(
                candidate => candidate.EmployeeId == employeeId
                             && BlockingStatuses.Contains(candidate.Status)
                             && candidate.From <= to
                             && candidate.To >= from
                             && (excludeId == null || candidate.Id != excludeId),
                cancellationToken)
            .ConfigureAwait(false);

        if (overlaps)
        {
            var range = FormattableString.Invariant($"{from:yyyy-MM-dd} - {to:yyyy-MM-dd}");

            throw new ConflictException(
                $"Bu calisanin {range} araliginda bekleyen veya onaylanmis bir izni var.");
        }
    }

    /// <summary>
    /// İlgili yılın bakiye satırını getirir; yoksa çalışanın yıllık izin hakkından oluşturur.
    /// <para>
    /// <b>Kaydetmez:</b> yeni satır yalnızca change tracker'a eklenir. Bakiye ile talep durumu
    /// çağıran handler'ın <b>tek</b> <c>SaveChangesAsync</c>'inde (dolayısıyla tek transaction'da)
    /// yazılır; biri yazılıp diğeri yazılmadan kalamaz.
    /// </para>
    /// <para>
    /// <c>HotelId</c> çalışandan alınır (aktif otelden değil): bakiye her zaman çalışanın oteline
    /// yazılır, Head Office konsolide modda onay verse bile.
    /// </para>
    /// </summary>
    public async Task<VacationBalance> GetOrCreateBalanceAsync(
        Employee employee,
        int year,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var balance = await FindBalanceAsync(employee.Id, year, cancellationToken).ConfigureAwait(false);
        if (balance is not null)
        {
            return balance;
        }

        balance = new VacationBalance
        {
            HotelId = employee.HotelId,
            EmployeeId = employee.Id,
            Year = year,
            EntitledDays = employee.AnnualLeaveDays,
            UsedDays = 0m,
            CarriedOverDays = 0m,
        };

        database.VacationBalances.Add(balance);

        return balance;
    }

    /// <summary>Kayıtlı bakiye satırı (takip edilen); yoksa null.</summary>
    public Task<VacationBalance?> FindBalanceAsync(
        Guid employeeId,
        int year,
        CancellationToken cancellationToken) =>
        database.VacationBalances
            .FirstOrDefaultAsync(
                balance => balance.EmployeeId == employeeId && balance.Year == year,
                cancellationToken);

    /// <summary>
    /// Bir yılın bakiye listesi. Satırı olmayan çalışan için bakiye <b>türetilir</b>
    /// (hak = <c>Employee.AnnualLeaveDays</c>, kullanılan = 0); okuma ucu veri yazmaz.
    /// <para>
    /// Kapsam: aktif otelin, istenen yılın başından <b>önce</b> işten ayrılmamış çalışanları —
    /// geçmiş yıllarda ayrılmış birinin bu yıl için bakiyesi yoktur.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<VacationBalanceResponse>> ListBalancesAsync(
        Guid? employeeId,
        int year,
        CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(year, 1, 1);

        var employees = database.Employees
            .Where(employee => employee.TerminatedOn == null || employee.TerminatedOn >= yearStart);

        if (employeeId is Guid id)
        {
            employees = employees.Where(employee => employee.Id == id);
        }

        var rows = await employees
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ThenBy(employee => employee.Id)
            .Select(employee => new BalanceRow(
                employee.Id,
                employee.FirstName + " " + employee.LastName,
                employee.AnnualLeaveDays,
                // Yil basina tek bakiye satiri vardir (unique index); alt sorgu tek satir doner.
                employee.VacationBalances.FirstOrDefault(balance => balance.Year == year)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (employeeId is not null && rows.Count == 0)
        {
            // Calisan yok, baska otelde ya da istenen yildan once ayrilmis.
            throw new NotFoundException("Calisan bulunamadi.");
        }

        return rows.ConvertAll(row => ToResponse(row, year));
    }

    private static VacationBalanceResponse ToResponse(BalanceRow row, int year)
    {
        var entitled = row.Balance?.EntitledDays ?? row.AnnualLeaveDays;
        var used = row.Balance?.UsedDays ?? 0m;
        var carriedOver = row.Balance?.CarriedOverDays ?? 0m;

        return new VacationBalanceResponse
        {
            Id = row.Balance?.Id,
            EmployeeId = row.EmployeeId,
            EmployeeName = row.EmployeeName,
            Year = year,
            EntitledDays = entitled,
            UsedDays = used,
            CarriedOverDays = carriedOver,
            RemainingDays = entitled + carriedOver - used,
        };
    }

    private static IQueryable<VacationRequest> ApplyFilters(
        IQueryable<VacationRequest> query,
        VacationListQuery filter)
    {
        if (filter.EmployeeId is Guid employeeId)
        {
            query = query.Where(request => request.EmployeeId == employeeId);
        }

        if (filter.Status is VacationStatus status)
        {
            query = query.Where(request => request.Status == status);
        }

        if (filter.Year is int year)
        {
            // Yil filtresi "kesisim"dir: yil sonunu asan (28.12 - 03.01) izin her iki yilda gorunur.
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);

            query = query.Where(request => request.From <= yearEnd && request.To >= yearStart);
        }

        if (filter.From is DateOnly from)
        {
            query = query.Where(request => request.To >= from);
        }

        if (filter.To is DateOnly to)
        {
            query = query.Where(request => request.From <= to);
        }

        return query;
    }

    /// <summary>Bakiye izdüşümünün ara satırı (çalışan + varsa kayıtlı bakiye).</summary>
    private sealed record BalanceRow(
        Guid EmployeeId,
        string EmployeeName,
        decimal AnnualLeaveDays,
        VacationBalance? Balance);
}

/// <summary>
/// İzin talebi izdüşümü — çalışan adı JOIN ile alınır (Include yerine izdüşüm: iki kolon okunur).
/// Çalışan soft-delete edilmişse JOIN boş döner ve ad boş metne düşülür.
/// </summary>
internal static class VacationQueryExtensions
{
    public static IQueryable<VacationRequestResponse> Project(this IQueryable<VacationRequest> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(request => new VacationRequestResponse
        {
            Id = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = (request.Employee.FirstName + " " + request.Employee.LastName) ?? string.Empty,
            From = request.From,
            To = request.To,
            RequestedDays = request.RequestedDays,
            Status = request.Status.ToString(),
            Reason = request.Reason,
            DecidedByUserId = request.ApprovedByUserId,
            DecidedAt = request.DecidedAt,
            DecisionNote = request.DecisionNote,
            CreatedAt = request.CreatedAt,
        });
    }
}
