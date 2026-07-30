namespace HotelCore.Application.Features.Availability.Common;

/// <summary>
/// Doluluk grid'inde <b>tek hücre</b> = bir oda + bir gece.
/// <para>
/// Hücreler <b>seyrek</b> (sparse) döner: yalnızca dolu geceler için hücre vardır. Boş geceler
/// yanıtta yer almaz (yüzlerce <c>null</c> taşımamak için); istemci <c>days</c> dizisini kolon
/// ekseni olarak kullanıp hücreleri <c>date</c> ile eşler.
/// </para>
/// </summary>
public sealed record OccupancyCellDto
{
    /// <summary>Gece (o gün <b>başlayan</b> konaklama gecesi).</summary>
    public DateOnly Date { get; init; }

    public Guid ReservationId { get; init; }

    public string ReservationNumber { get; init; } = string.Empty;

    public string GuestName { get; init; } = string.Empty;

    /// <summary>Durum enum <b>adı</b> — grid'de görsel stile (örn. Option = kesikli çizgi) karşılık gelir.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Bu gece konaklamanın <b>ilk</b> gecesi mi (misafir bu gün giriş yapar).</summary>
    public bool IsArrival { get; init; }

    /// <summary>
    /// Bu gece konaklamanın <b>son</b> gecesi mi. Misafir <b>ertesi sabah</b> (<c>checkOut</c>)
    /// çıkar; yarı açık aralık gereği çıkış günü için hücre üretilmez — bu yüzden "çıkış"
    /// bayrağı son geceye konur ve grid çubuğu doğru yerde biter.
    /// </summary>
    public bool IsDeparture { get; init; }
}

/// <summary>Grid'in bir satırı: oda + o odaya ait dolu hücreler.</summary>
public sealed record OccupancyRoomRowDto
{
    public Guid RoomId { get; init; }

    public string RoomNumber { get; init; } = string.Empty;

    public int Floor { get; init; }

    public Guid RoomTypeId { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    /// <summary>Servis dışı oda — grid'de kapalı gösterilir, müsait sayılmaz.</summary>
    public bool IsOutOfOrder { get; init; }

    public IReadOnlyList<OccupancyCellDto> Cells { get; init; } = [];
}

/// <summary>Grid özeti (doluluk oranı raporu için).</summary>
/// <param name="RoomCount">Otelin oda sayısı.</param>
/// <param name="Days">Aralıktaki gece sayısı.</param>
/// <param name="RoomNights">Toplam kapasite = oda × gece.</param>
/// <param name="OccupiedRoomNights">Dolu oda-gece sayısı (iptal/no-show sayılmaz).</param>
/// <param name="OccupancyRate">Doluluk oranı, yüzde (2 haneye yuvarlı).</param>
public sealed record OccupancySummaryDto(
    int RoomCount,
    int Days,
    int RoomNights,
    int OccupiedRoomNights,
    decimal OccupancyRate);

/// <summary>
/// <c>GET /api/v1/occupancy?from=&amp;to=</c> yanıtı — <b>oda × gün</b> doluluk matrisi.
/// <para>
/// Aralık yarı açıktır <c>[from, to)</c>: <c>days</c> dizisi <c>from</c>'dan başlar ve
/// <c>to</c>'yu <b>içermez</b> (son gece <c>to - 1</c>). Aralık
/// <see cref="AvailabilityLimits.MaxOccupancyRangeDays"/> günü aşarsa <b>400</b> döner.
/// </para>
/// </summary>
public sealed record OccupancyResponse
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    /// <summary>Kolon ekseni: aralıktaki geceler (<c>from</c> dahil, <c>to</c> hariç).</summary>
    public IReadOnlyList<DateOnly> Days { get; init; } = [];

    /// <summary>Satır ekseni: odalar (kat, sonra doğal numara sırasında).</summary>
    public IReadOnlyList<OccupancyRoomRowDto> Rooms { get; init; } = [];

    public OccupancySummaryDto Summary { get; init; } = new(0, 0, 0, 0, 0m);
}
