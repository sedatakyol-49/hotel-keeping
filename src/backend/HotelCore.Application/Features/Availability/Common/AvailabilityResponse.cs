namespace HotelCore.Application.Features.Availability.Common;

/// <summary>
/// Müsait tek oda. <b>Fiyat alanı içermez:</b> nihai tutar rezervasyon oluşturulurken sunucuda
/// hesaplanır (fiyat mantığı tek yerde kalsın); liste fiyatı gerekiyorsa
/// <c>GET /room-types</c> kullanılır.
/// </summary>
public sealed record AvailableRoomDto
{
    public Guid RoomId { get; init; }

    public string RoomNumber { get; init; } = string.Empty;

    public int Floor { get; init; }

    public Guid RoomTypeId { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    /// <summary>Oda tipinin kişi kapasitesi — sihirbazda kişi sayısı filtresi için.</summary>
    public int Capacity { get; init; }
}

/// <summary>Oda tipi bazında müsait oda sayısı (sihirbazın ilk adımı için özet).</summary>
/// <param name="RoomTypeId">Oda tipi kimliği.</param>
/// <param name="RoomTypeCode">Oda tipi kodu.</param>
/// <param name="AvailableRoomCount">Aralık boyunca tamamı müsait oda sayısı.</param>
public sealed record AvailabilityByRoomTypeDto(
    Guid RoomTypeId,
    string RoomTypeCode,
    int AvailableRoomCount);

/// <summary>
/// <c>GET /api/v1/availability?from=&amp;to=&amp;roomTypeId=</c> yanıtı.
/// <para>
/// <b>Aralık yarı açıktır</b> <c>[from, to)</c>: <c>to</c> günü çıkış günüdür ve o gece için
/// oda aranmaz. Bir oda ancak aralıktaki <b>tüm</b> geceler boşsa müsait sayılır; servis dışı
/// (<c>isOutOfOrder</c>) odalar hiç listelenmez.
/// </para>
/// </summary>
public sealed record AvailabilityResponse
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    /// <summary>Gece sayısı = <c>to - from</c>.</summary>
    public int Nights { get; init; }

    /// <summary>Uygulanan oda tipi filtresi (yoksa <c>null</c>).</summary>
    public Guid? RoomTypeId { get; init; }

    /// <summary>Filtreye uyan toplam oda sayısı (servis dışı odalar dâhil).</summary>
    public int TotalRoomCount { get; init; }

    /// <summary>Filtreye uyan servis dışı oda sayısı — müsait sayılmazlar.</summary>
    public int OutOfOrderRoomCount { get; init; }

    /// <summary>Aralık boyunca tamamı müsait oda sayısı.</summary>
    public int AvailableRoomCount { get; init; }

    public IReadOnlyList<AvailabilityByRoomTypeDto> ByRoomType { get; init; } = [];

    public IReadOnlyList<AvailableRoomDto> Rooms { get; init; } = [];
}
