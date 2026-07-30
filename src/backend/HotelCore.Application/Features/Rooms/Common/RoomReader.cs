using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda yanıtlarının tek üretim noktası (liste, detay, pano ve yazma uçlarının gövdesi).
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class RoomReader(IAppDbContext database, IMapper mapper, TranslationService translations)
{
    /// <summary>Sayfalı + filtreli oda listesi; sıralama doğal numara sırasındadır.</summary>
    public async Task<PagedResult<RoomResponse>> ListAsync(RoomListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplyFilters(database.Rooms, query);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await filtered
            .OrderByFloorThenNumber()
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .ProjectToRow()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await ToResponsesAsync(rows, cancellationToken).ConfigureAwait(false);

        return new PagedResult<RoomResponse>(items, query.Paging.Page, query.Paging.PageSize, totalCount);
    }

    /// <summary>Tek oda; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<RoomResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.Rooms
            .Where(room => room.Id == id)
            .ProjectToRow()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), id);

        var responses = await ToResponsesAsync([row], cancellationToken).ConfigureAwait(false);

        return responses[0];
    }

    /// <summary>
    /// Kat hizmetleri panosu: kat bazlı gruplama + durum sayaçları.
    /// <para>
    /// Sıralama ve filtreleme SQL'de yapılır; gruplama ve sayaçlar zaten okunmuş satırlar
    /// üzerinde tek geçişte hesaplanır (ayrı bir GROUP BY sorgusu daha fazla gidiş-dönüş demekti).
    /// Yanıtta <b>hiçbir finansal alan yoktur</b> — bkz. <see cref="RoomBoardItemDto"/>.
    /// </para>
    /// </summary>
    public async Task<RoomBoardResponse> GetBoardAsync(CancellationToken cancellationToken)
    {
        var rows = await database.Rooms
            .OrderByFloorThenNumber()
            .ProjectToBoardRow()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Satırlar SQL'de kat + doğal numara sırasında geldiği için GroupBy sırayı korur.
        var floors = rows
            .GroupBy(row => row.Floor)
            .Select(group => new RoomBoardFloorDto(
                group.Key,
                group.Select(row => mapper.Map<RoomBoardItemDto>(row)).ToList()))
            .ToList();

        var summary = new RoomBoardSummaryDto(
            rows.Count(row => row.HousekeepingStatus is HousekeepingStatus.Clean),
            rows.Count(row => row.HousekeepingStatus is HousekeepingStatus.Dirty),
            rows.Count(row => row.HousekeepingStatus is HousekeepingStatus.Inspected),
            rows.Count(row => row.HousekeepingStatus is HousekeepingStatus.OutOfOrder),
            rows.Count);

        return new RoomBoardResponse(floors, summary);
    }

    private static IQueryable<Room> ApplyFilters(IQueryable<Room> query, RoomListQuery filter)
    {
        if (filter.RoomTypeId is Guid roomTypeId)
        {
            query = query.Where(room => room.RoomTypeId == roomTypeId);
        }

        if (filter.Floor is int floor)
        {
            query = query.Where(room => room.Floor == floor);
        }

        if (filter.HousekeepingStatus is HousekeepingStatus status)
        {
            query = query.Where(room => room.HousekeepingStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Büyük/küçük harf duyarsız "contains": arama terimi C# tarafında küçültülür,
            // kolon SQL'de lower(...) ile küçültülür. LIKE yerine strpos'a çevrildiği için
            // '%' / '_' gibi karakterlerin joker etkisi yoktur (kaçış gerekmez).
            var term = filter.Search.Trim().ToLowerInvariant();

            // CA1304/CA1311/CA1862 bastırılır: kültür parametreli ve StringComparison'lı aşırı
            // yüklemeleri EF Core SQL'e ÇEVİREMEZ. Parametresiz ToLower() burada .NET'te değil
            // PostgreSQL'de lower(...) olarak çalışır; sonuç veritabanı collation'ına bağlıdır.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(room => room.Number.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }

    /// <summary>
    /// Satırları DTO'ya çevirir (Mapster) ve <c>roomTypeName</c>'i aktif dile göre çözümler:
    /// oda tipi adı çok dillidir, çeviri yoksa entity'deki varsayılan ada düşülür.
    /// Çeviriler ilgili oda tipleri için TEK sorguda alınır (N+1 yok).
    /// </summary>
    private async Task<IReadOnlyList<RoomResponse>> ToResponsesAsync(
        List<RoomRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var roomTypeIds = rows.ConvertAll(row => row.RoomTypeId);
        var resolved = await translations
            .GetForCultureAsync(
                TranslationEntityTypes.RoomType,
                roomTypeIds,
                RequestCulture.Current,
                cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row =>
        {
            var fields = TranslationService.FieldsFor(resolved, row.RoomTypeId);

            return mapper.Map<RoomResponse>(row) with
            {
                RoomTypeName = TranslationService.Resolve(fields, TranslationFields.Name, row.RoomTypeName)
                               ?? row.RoomTypeName
            };
        });
    }
}
