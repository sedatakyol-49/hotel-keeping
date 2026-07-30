using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.TimeEntries.Common;

/// <summary>
/// Zaman kaydı yanıtlarının tek üretim noktası + açık kayıt (ClockOut = null) kuralları.
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class TimeEntryReader(IAppDbContext database)
{
    public async Task<PagedResult<TimeEntryResponse>> ListAsync(
        TimeEntryListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplyFilters(database.TimeEntries, query);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await filtered
            // En yeni mesai en ustte; esitlikte Id ile kararli siralama.
            .OrderByDescending(entry => entry.ClockIn)
            .ThenBy(entry => entry.Id)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .ProjectToRow()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<TimeEntryResponse>(
            rows.ConvertAll(row => row.ToResponse()),
            query.Paging.Page,
            query.Paging.PageSize,
            totalCount);
    }

    /// <summary>Tek kayıt; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<TimeEntryResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.TimeEntries
            .Where(candidate => candidate.Id == id)
            .ProjectToRow()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row?.ToResponse() ?? throw new NotFoundException(nameof(TimeEntry), id);
    }

    public async Task<TimeEntry> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await database.TimeEntries
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entry ?? throw new NotFoundException(nameof(TimeEntry), id);
    }

    /// <summary>
    /// Çalışanın açık kaydını (çıkış yapılmamış mesai) döndürür; yoksa <b>409</b>.
    /// Çıkış yapılacak kaydı istemci seçmez — açık kayıt tanım gereği tektir.
    /// </summary>
    public async Task<TimeEntry> GetOpenEntryAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var open = await FindOpenEntryAsync(employeeId, excludeId: null, cancellationToken)
            .ConfigureAwait(false);

        return open ?? throw new ConflictException(
            "Bu calisanin acik bir mesai kaydi yok; once giris (clock-in) yapilmalidir.");
    }

    /// <summary>
    /// Açık kayıt varsa <b>409</b>. İkinci bir clock-in ya da bir kaydın yeniden "açık" hâle
    /// getirilmesi engellenir; aksi hâlde hangi kaydın çıkışının yapılacağı belirsizleşirdi.
    /// </summary>
    public async Task EnsureNoOpenEntryAsync(
        Guid employeeId,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var open = await FindOpenEntryAsync(employeeId, excludeId, cancellationToken)
            .ConfigureAwait(false);

        if (open is not null)
        {
            throw new ConflictException(
                "Bu calisanin devam eden bir mesai kaydi var; once cikis (clock-out) yapilmalidir.");
        }
    }

    private Task<TimeEntry?> FindOpenEntryAsync(
        Guid employeeId,
        Guid? excludeId,
        CancellationToken cancellationToken) =>
        database.TimeEntries
            .OrderByDescending(entry => entry.ClockIn)
            .FirstOrDefaultAsync(
                entry => entry.EmployeeId == employeeId
                         && entry.ClockOut == null
                         && (excludeId == null || entry.Id != excludeId),
                cancellationToken);

    private static IQueryable<TimeEntry> ApplyFilters(
        IQueryable<TimeEntry> query,
        TimeEntryListQuery filter)
    {
        if (filter.EmployeeId is Guid employeeId)
        {
            query = query.Where(entry => entry.EmployeeId == employeeId);
        }

        // Tarih filtresi giris anina (ClockIn) ve UTC gun sinirlarina gore uygulanir: kayitlar
        // UTC saklandigi icin gunun tanimi da UTC'dir (yerel gune cevirme sunum katmaninin isi).
        if (filter.From is DateOnly from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(entry => entry.ClockIn >= start);
        }

        if (filter.To is DateOnly to)
        {
            var endExclusive = new DateTimeOffset(
                to.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            query = query.Where(entry => entry.ClockIn < endExclusive);
        }

        return query;
    }
}
