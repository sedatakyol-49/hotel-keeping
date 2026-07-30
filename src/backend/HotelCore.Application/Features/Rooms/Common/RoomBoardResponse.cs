namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Kat hizmetleri panosundaki tek oda kartı.
/// <para>
/// <b>RBAC (architecture.md §7 / api-contracts.md):</b> Housekeeping rolü fiyat/ciro görmez.
/// Bu yüzden pano DTO'sunda <c>basePrice</c>, <c>currency</c> gibi <b>hiçbir finansal alan
/// TANIMLI DEĞİLDİR</b> — alan gizlenmiyor, DTO'da hiç yok; sorgu da bu kolonları seçmez
/// (bkz. <c>RoomBoardRow</c>). Frontend'de gizlemek yeterli olmadığı için kural burada uygulanır.
/// </para>
/// </summary>
public sealed record RoomBoardItemDto
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary>Yalnızca kod gösterilir; oda tipi adı/fiyatı panoda gerekmez.</summary>
    public string RoomTypeCode { get; init; } = string.Empty;

    public string HousekeepingStatus { get; init; } = string.Empty;

    public bool IsOutOfOrder { get; init; }

    public string? Note { get; init; }
}

/// <summary>Kat bazlı gruplama: kat numarası + o kattaki odalar (doğal numara sırasında).</summary>
/// <param name="Floor">Kat numarası.</param>
/// <param name="Rooms">Kattaki odalar.</param>
public sealed record RoomBoardFloorDto(int Floor, IReadOnlyList<RoomBoardItemDto> Rooms);

/// <summary>Pano sayaçları. <c>OutOfOrder</c> durumundaki odalar ayrı sayılır.</summary>
/// <param name="Clean">Temiz oda sayısı.</param>
/// <param name="Dirty">Kirli oda sayısı.</param>
/// <param name="Inspected">Kontrol edilmiş oda sayısı.</param>
/// <param name="OutOfOrder">Servis dışı oda sayısı.</param>
/// <param name="Total">Toplam oda sayısı.</param>
public sealed record RoomBoardSummaryDto(int Clean, int Dirty, int Inspected, int OutOfOrder, int Total);

/// <summary><c>GET /api/v1/rooms/board</c> yanıtı: kat listesi + özet sayaçlar.</summary>
/// <param name="Floors">Kat bazlı gruplar (kat numarasına göre artan).</param>
/// <param name="Summary">Durum sayaçları.</param>
public sealed record RoomBoardResponse(IReadOnlyList<RoomBoardFloorDto> Floors, RoomBoardSummaryDto Summary);
