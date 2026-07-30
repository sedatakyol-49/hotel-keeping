namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Raporun hangi kapsamda hesaplandığı. <b>Her rapor yanıtında bulunur</b> — konsolide bir
/// sayıyı tek otel sanmak (veya tersi) en pahalı yanlış okumadır.
/// </summary>
public sealed record ReportScopeDto
{
    /// <summary><c>Hotel</c> veya <c>Consolidated</c> (bkz. <see cref="ReportScopeModes"/>).</summary>
    public string Mode { get; init; } = ReportScopeModes.Hotel;

    /// <summary>Tek otel modunda otelin kimliği; konsolide modda <c>null</c>.</summary>
    public Guid? HotelId { get; init; }

    /// <summary>Rapora giren otel sayısı.</summary>
    public int HotelCount { get; init; }

    /// <summary>
    /// Ortak para birimi. Konsolide modda oteller farklı para birimleri kullanıyorsa
    /// <c>null</c> olur ve <see cref="HasMixedCurrencies"/> <c>true</c> döner.
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>
    /// <c>true</c> ise kapsamdaki oteller <b>farklı</b> para birimleri kullanıyor; üst seviye
    /// para toplamları farklı birimlerin aritmetik toplamıdır ve <b>kullanılmamalıdır</b> —
    /// otel kırılımı (<c>byHotel</c>) esas alınmalıdır.
    /// </summary>
    public bool HasMixedCurrencies { get; init; }
}

/// <summary>Doluluk raporunun bir günü (grafik ekseni).</summary>
public sealed record OccupancyDayDto
{
    /// <summary>Gün — o gün <b>başlayan</b> geceyi temsil eder.</summary>
    public DateOnly Date { get; init; }

    /// <summary>O gece satılan oda sayısı.</summary>
    public int SoldRoomNights { get; init; }

    /// <summary>O gece müsait oda sayısı (servis dışı odalar düşülmüş).</summary>
    public int AvailableRoomNights { get; init; }

    /// <summary>Doluluk oranı, yüzde (2 ondalık).</summary>
    public decimal OccupancyRate { get; init; }
}

/// <summary>Doluluk raporunun otel kırılımı (konsolide modda anlamlı, tek otelde tek satır).</summary>
public sealed record OccupancyByHotelDto
{
    public Guid HotelId { get; init; }

    public string HotelName { get; init; } = string.Empty;

    public int RoomCount { get; init; }

    public int OutOfOrderRoomCount { get; init; }

    public int PhysicalRoomNights { get; init; }

    public int OutOfOrderRoomNights { get; init; }

    public int AvailableRoomNights { get; init; }

    public int SoldRoomNights { get; init; }

    public decimal OccupancyRate { get; init; }
}

/// <summary>
/// <c>GET /api/v1/reports/occupancy?from=&amp;to=</c> yanıtı.
/// <para>
/// Aralık <b>kapalıdır</b> (<c>from</c> ve <c>to</c> dâhil); sayılan geceler bu günlerde
/// <b>başlayan</b> gecelerdir — çıkış günü gece saymaz (rezervasyon modülünün yarı açık
/// aralık kararı). Tanımların tamamı <c>docs/api-contracts-reports.md</c> ve
/// <see cref="ReportDefinitions"/> içindedir.
/// </para>
/// </summary>
public sealed record OccupancyReportResponse
{
    public DateOnly From { get; init; }

    /// <summary>Son gün — <b>dâhil</b>.</summary>
    public DateOnly To { get; init; }

    /// <summary>Aralıktaki gün (= gece) sayısı.</summary>
    public int DayCount { get; init; }

    public ReportScopeDto Scope { get; init; } = new();

    /// <summary>Kapsamdaki toplam oda sayısı (servis dışı odalar dâhil).</summary>
    public int RoomCount { get; init; }

    /// <summary>Servis dışı (<c>isOutOfOrder</c>) oda sayısı.</summary>
    public int OutOfOrderRoomCount { get; init; }

    /// <summary>Fiziksel kapasite = oda sayısı × gün sayısı.</summary>
    public int PhysicalRoomNights { get; init; }

    /// <summary>Servis dışı kapasite = servis dışı oda sayısı × gün sayısı.</summary>
    public int OutOfOrderRoomNights { get; init; }

    /// <summary>
    /// Satılabilir kapasite = <c>physical − outOfOrder</c>. Doluluk ve RevPAR'ın <b>paydası</b>.
    /// </summary>
    public int AvailableRoomNights { get; init; }

    /// <summary>Satılan oda-gece (iptal/no-show sayılmaz).</summary>
    public int SoldRoomNights { get; init; }

    /// <summary>
    /// <c>sold / available × 100</c>. Servis dışı bayrağı tarihsiz olduğu için <b>%100'ü
    /// aşabilir</b> (geçmişte dolu, bugün servis dışı oda) — değer kırpılmaz.
    /// </summary>
    public decimal OccupancyRate { get; init; }

    /// <summary>Gün bazında seri (grafik için); <c>dayCount</c> kadar eleman.</summary>
    public IReadOnlyList<OccupancyDayDto> Daily { get; init; } = [];

    /// <summary>Otel kırılımı — tek otel modunda tek eleman içerir.</summary>
    public IReadOnlyList<OccupancyByHotelDto> ByHotel { get; init; } = [];
}
