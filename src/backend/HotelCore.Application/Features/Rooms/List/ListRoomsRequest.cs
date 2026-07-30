using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.List;

/// <summary>
/// <c>GET /api/v1/rooms</c> sorgu parametreleri:
/// <c>?page=1&amp;pageSize=20&amp;roomTypeId=&amp;floor=&amp;housekeepingStatus=&amp;search=</c>.
/// Sıralama sabittir: <c>floor</c>, sonra <c>number</c> (doğal/numerik).
/// </summary>
public sealed record ListRoomsRequest : IRequest<PagedResult<RoomResponse>>
{
    /// <summary>1 tabanlı sayfa numarası.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Sayfa boyutu; sınırlar <see cref="PageQuery"/> tarafından uygulanır.</summary>
    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    /// <summary>Oda tipi filtresi.</summary>
    public Guid? RoomTypeId { get; init; }

    /// <summary>Kat filtresi.</summary>
    public int? Floor { get; init; }

    /// <summary>Kat hizmetleri durumu filtresi (<c>Clean | Dirty | Inspected | OutOfOrder</c>).</summary>
    public HousekeepingStatus? HousekeepingStatus { get; init; }

    /// <summary>Oda numarasında büyük/küçük harf duyarsız arama (contains).</summary>
    public string? Search { get; init; }

    /// <summary>
    /// Sınırları uygulanmış sayfalama (page &lt; 1 veya pageSize &gt; 200 sessizce düzeltilir).
    /// <para>
    /// <b>internal:</b> public olsaydı ApiExplorer bunu bağlanabilir bir sorgu parametresi sanıp
    /// OpenAPI şemasına <c>Paging.Page</c>/<c>Paging.Skip</c> gibi sözleşmede olmayan alanlar
    /// eklerdi (frontend client üretimi bu şemadan yapılıyor).
    /// </para>
    /// </summary>
    internal PageQuery ToPageQuery() => new() { Page = Page, PageSize = PageSize };
}
