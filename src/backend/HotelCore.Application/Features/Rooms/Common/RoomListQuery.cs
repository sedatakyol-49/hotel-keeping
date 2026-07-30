using HotelCore.Application.Common.Models;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda listesinin normalize edilmiş sorgu parametreleri. Handler, HTTP isteğini bu tipe çevirir;
/// böylece <see cref="RoomReader"/> sunum katmanının şeklinden bağımsız kalır.
/// </summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama (bkz. <see cref="PageQuery"/>).</param>
/// <param name="RoomTypeId">Oda tipi filtresi.</param>
/// <param name="Floor">Kat filtresi.</param>
/// <param name="HousekeepingStatus">Kat hizmetleri durumu filtresi.</param>
/// <param name="Search">Oda numarasında büyük/küçük harf duyarsız "contains" arama.</param>
internal sealed record RoomListQuery(
    PageQuery Paging,
    Guid? RoomTypeId,
    int? Floor,
    HousekeepingStatus? HousekeepingStatus,
    string? Search);
