using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Raporların tek üretim noktası: kapsam çözümü → toplulaştırılmış SQL sorguları →
/// gün/kanal/otel eksenlerine yayma → yanıt.
///
/// <para><b>Konsolide mod kararı:</b> müsaitlik ve doluluk <i>grid</i>'i aktif otel zorunlu
/// kılar (bir takvim tek bir otele aittir). <b>Raporlar bunu zorunlu kılmaz</b> — konsolide KPI
/// Head Office'in var olma sebebidir ve portföy doluluğu (<c>Σsatılan / Σmüsait</c>) otelcilikte
/// standart bir büyüklüktür. Ancak konsolide bir ADR yanıltıcı olabilir (farklı segment, farklı
/// para birimi), bu yüzden:
/// <list type="bullet">
///   <item>Yanıtta <c>scope</c> bulunur: <c>Hotel</c> mi <c>Consolidated</c> mı, kaç otel,
///   hangi para birimi.</item>
///   <item>Her rapor <c>byHotel</c> kırılımı döndürür (tek otel modunda tek satır) — konsolide
///   sayı her zaman otel bazına ayrıştırılabilir.</item>
///   <item>Oteller farklı para birimleri kullanıyorsa <c>scope.currency = null</c> ve
///   <c>hasMixedCurrencies = true</c> olur: üst seviye para toplamları anlamsızdır, tüketici
///   <c>byHotel</c>'e bakmalıdır. Sayı gizlenmez, <b>etiketlenir</b>.</item>
/// </list></para>
///
/// <para><b>Tenant izolasyonu:</b> kapsam <c>HotelReader.AccessibleHotels()</c> üzerinden
/// çözülür ve tüm sorgular bu otel kimlikleriyle <b>ek olarak</b> daraltılır; global query
/// filter hiçbir yerde atlanmaz.</para>
/// </summary>
internal sealed class ReportReader(ReportDataSource source, ILogger<ReportReader> logger)
{
    /// <summary>RevPAR çapraz kontrolünde kabul edilen fark (yuvarlama payı).</summary>
    private const decimal RevParTolerance = 0.01m;

    public async Task<OccupancyReportResponse> GetOccupancyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var period = new ReportPeriod(from, to);
        var context = await LoadAsync(period, includeRevenue: false, cancellationToken).ConfigureAwait(false);

        var capacity = context.Capacity;
        var aggregate = context.Aggregate;

        return new OccupancyReportResponse
        {
            From = period.From,
            To = period.To,
            DayCount = period.DayCount,
            Scope = context.Scope,
            RoomCount = capacity.RoomCount,
            OutOfOrderRoomCount = capacity.OutOfOrderRoomCount,
            PhysicalRoomNights = capacity.RoomCount * period.DayCount,
            OutOfOrderRoomNights = capacity.OutOfOrderRoomCount * period.DayCount,
            AvailableRoomNights = capacity.AvailableRoomCount * period.DayCount,
            SoldRoomNights = aggregate.Total.SoldRoomNights,
            OccupancyRate = ReportMath.Percent(
                aggregate.Total.SoldRoomNights,
                capacity.AvailableRoomCount * period.DayCount),
            Daily = BuildOccupancyDays(period, aggregate, capacity.AvailableRoomCount),
            ByHotel = BuildOccupancyByHotel(period, context),
        };
    }

    public async Task<RevenueReportResponse> GetRevenueAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var period = new ReportPeriod(from, to);
        var context = await LoadAsync(period, includeRevenue: true, cancellationToken).ConfigureAwait(false);

        var capacity = context.Capacity;
        var totals = context.Aggregate.Total;

        var availableRoomNights = capacity.AvailableRoomCount * period.DayCount;
        var occupancyRate = ReportMath.Percent(totals.SoldRoomNights, availableRoomNights);
        var adrNet = ReportMath.PerUnit(totals.RoomNet, totals.SoldRoomNights);
        var revParNet = ReportMath.PerUnit(totals.RoomNet, availableRoomNights);

        VerifyRevParIdentity(revParNet, adrNet, occupancyRate);

        return new RevenueReportResponse
        {
            From = period.From,
            To = period.To,
            DayCount = period.DayCount,
            Scope = context.Scope,
            SoldRoomNights = totals.SoldRoomNights,
            AvailableRoomNights = availableRoomNights,
            OutOfOrderRoomNights = capacity.OutOfOrderRoomCount * period.DayCount,
            PhysicalRoomNights = capacity.RoomCount * period.DayCount,
            OccupancyRate = occupancyRate,
            RoomRevenue = totals.RoomRevenue(),
            ExtraRevenue = totals.ExtraRevenue(),
            TotalRevenue = totals.TotalRevenue(),
            CityTaxCollected = ReportMath.Round(totals.CityTax),
            AdrNet = adrNet,
            AdrGross = ReportMath.PerUnit(totals.RoomGross, totals.SoldRoomNights),
            RevParNet = revParNet,
            RevParGross = ReportMath.PerUnit(totals.RoomGross, availableRoomNights),
            UnbilledRoomRevenueGross = ReportMath.Round(totals.UnbilledGross),
            OtherInvoicedRevenue = BuildOtherRevenue(context.OtherInvoiceRows),
            ByChannel = BuildByChannel(context.Aggregate, totals.RoomNet),
            ByHotel = BuildRevenueByHotel(period, context),
            Daily = BuildRevenueDays(period, context.Aggregate, capacity.AvailableRoomCount),
        };
    }

    // ---------------------------------------------------------------------------------------
    // Veri yukleme
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Raporun tüm girdilerini yükler. Sorgu sayısı <b>sabittir</b>: doluluk 3, ciro 6
    /// (kapsam otelleri, oda kapasitesi, konaklama kovaları [+ faturalanmış alt küme,
    /// fatura tutarları, dağıtılamayan fatura tutarları]).
    /// </summary>
    private async Task<ReportContext> LoadAsync(
        ReportPeriod period,
        bool includeRevenue,
        CancellationToken cancellationToken)
    {
        var hotels = await source.GetScopeHotelsAsync(cancellationToken).ConfigureAwait(false);
        var hotelIds = hotels.Select(hotel => hotel.Id).ToArray();

        var capacityRows = await source
            .GetRoomCapacityAsync(hotelIds, cancellationToken)
            .ConfigureAwait(false);

        var stayGroups = await source
            .GetStayGroupsAsync(hotelIds, period, onlyBilled: false, cancellationToken)
            .ConfigureAwait(false);

        var buckets = new Dictionary<StayBucketKey, StayBucket>();

        foreach (var row in stayGroups)
        {
            var bucket = GetOrAddBucket(buckets, row.HotelId, row.CheckIn, row.CheckOut, row.Channel);
            bucket.ReservationCount += row.ReservationCount;
            bucket.ReservationAmount += row.ReservationAmount;
        }

        IReadOnlyList<OtherInvoiceRow> otherRows = [];

        if (includeRevenue)
        {
            var billedGroups = await source
                .GetStayGroupsAsync(hotelIds, period, onlyBilled: true, cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in billedGroups)
            {
                var bucket = GetOrAddBucket(buckets, row.HotelId, row.CheckIn, row.CheckOut, row.Channel);
                bucket.BilledReservationAmount += row.ReservationAmount;
            }

            var moneyRows = await source
                .GetStayInvoiceAmountsAsync(hotelIds, period, cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in moneyRows)
            {
                var bucket = GetOrAddBucket(buckets, row.HotelId, row.CheckIn, row.CheckOut, row.Channel);

                switch (row.Type)
                {
                    case InvoiceLineType.RoomCharge:
                        bucket.RoomNet += row.Net;
                        bucket.RoomVat += row.Vat;
                        break;
                    case InvoiceLineType.Extra:
                        bucket.ExtraNet += row.Net;
                        bucket.ExtraVat += row.Vat;
                        break;
                    case InvoiceLineType.CityTax:
                        // Kurtaxe gelir DEGILDIR: ayri birikir, hicbir ciro toplamina girmez.
                        bucket.CityTax += row.Net + row.Vat;
                        break;
                    default:
                        break;
                }
            }

            otherRows = await source
                .GetOtherInvoiceAmountsAsync(hotelIds, period, cancellationToken)
                .ConfigureAwait(false);
        }

        return new ReportContext(
            hotels,
            BuildScope(hotels),
            BuildCapacity(hotels, capacityRows),
            ReportAggregator.Build(period, buckets.Values),
            otherRows);
    }

    private static StayBucket GetOrAddBucket(
        Dictionary<StayBucketKey, StayBucket> buckets,
        Guid hotelId,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationChannel channel)
    {
        var key = new StayBucketKey(hotelId, checkIn, checkOut, channel);

        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = new StayBucket(hotelId, checkIn, checkOut, channel);
            buckets[key] = bucket;
        }

        return bucket;
    }

    // ---------------------------------------------------------------------------------------
    // Kapsam ve kapasite
    // ---------------------------------------------------------------------------------------

    private ReportScopeDto BuildScope(IReadOnlyList<ReportHotelInfo> hotels)
    {
        var currencies = hotels
            .Select(hotel => hotel.Currency)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var isSingleHotel = source.IsSingleHotelScope;

        return new ReportScopeDto
        {
            Mode = isSingleHotel ? ReportScopeModes.Hotel : ReportScopeModes.Consolidated,
            HotelId = isSingleHotel ? hotels.Select(hotel => (Guid?)hotel.Id).FirstOrDefault() : null,
            HotelCount = hotels.Count,
            Currency = currencies.Count == 1 ? currencies[0] : null,
            HasMixedCurrencies = currencies.Count > 1,
        };
    }

    /// <summary>
    /// Kapasite satırlarını otel kırılımıyla birleştirir. Hiç odası olmayan otel de kapsamda
    /// kalır (0 oda), böylece <c>byHotel</c> ile <c>scope.hotelCount</c> tutarlı olur.
    /// </summary>
    private static ReportCapacity BuildCapacity(
        IReadOnlyList<ReportHotelInfo> hotels,
        IReadOnlyList<RoomCapacityRow> rows)
    {
        var byHotel = hotels.ToDictionary(
            hotel => hotel.Id,
            _ => new RoomCapacityRow(Guid.Empty, 0, 0));

        foreach (var row in rows)
        {
            byHotel[row.HotelId] = row;
        }

        return new ReportCapacity(
            rows.Sum(row => row.RoomCount),
            rows.Sum(row => row.OutOfOrderRoomCount),
            byHotel);
    }

    // ---------------------------------------------------------------------------------------
    // Yanit parcalari
    // ---------------------------------------------------------------------------------------

    private static List<OccupancyDayDto> BuildOccupancyDays(
        ReportPeriod period,
        ReportAggregate aggregate,
        int availableRooms)
    {
        var days = new List<OccupancyDayDto>(period.DayCount);

        for (var index = 0; index < period.DayCount; index++)
        {
            var day = aggregate.ByDay[index];

            days.Add(new OccupancyDayDto
            {
                Date = period.DateAt(index),
                SoldRoomNights = day.SoldRoomNights,
                AvailableRoomNights = availableRooms,
                OccupancyRate = ReportMath.Percent(day.SoldRoomNights, availableRooms),
            });
        }

        return days;
    }

    private static List<OccupancyByHotelDto> BuildOccupancyByHotel(ReportPeriod period, ReportContext context)
    {
        var result = new List<OccupancyByHotelDto>(context.Hotels.Count);

        foreach (var hotel in context.Hotels)
        {
            var capacity = context.Capacity.ByHotel[hotel.Id];
            var available = (capacity.RoomCount - capacity.OutOfOrderRoomCount) * period.DayCount;
            var sold = context.Aggregate.ByHotel.TryGetValue(hotel.Id, out var totals)
                ? totals.SoldRoomNights
                : 0;

            result.Add(new OccupancyByHotelDto
            {
                HotelId = hotel.Id,
                HotelName = hotel.Name,
                RoomCount = capacity.RoomCount,
                OutOfOrderRoomCount = capacity.OutOfOrderRoomCount,
                PhysicalRoomNights = capacity.RoomCount * period.DayCount,
                OutOfOrderRoomNights = capacity.OutOfOrderRoomCount * period.DayCount,
                AvailableRoomNights = available,
                SoldRoomNights = sold,
                OccupancyRate = ReportMath.Percent(sold, available),
            });
        }

        return result;
    }

    private static OtherInvoicedRevenueDto BuildOtherRevenue(IReadOnlyList<OtherInvoiceRow> rows)
    {
        var totals = new ReportTotals();

        foreach (var row in rows)
        {
            switch (row.Type)
            {
                case InvoiceLineType.RoomCharge:
                    totals.RoomNet += row.Net;
                    totals.RoomVat += row.Vat;
                    break;
                case InvoiceLineType.Extra:
                    totals.ExtraNet += row.Net;
                    totals.ExtraVat += row.Vat;
                    break;
                case InvoiceLineType.CityTax:
                    totals.CityTax += row.Net + row.Vat;
                    break;
                default:
                    break;
            }
        }

        return new OtherInvoicedRevenueDto
        {
            Room = totals.RoomRevenue(),
            Extra = totals.ExtraRevenue(),
            Total = totals.TotalRevenue(),
            CityTaxCollected = ReportMath.Round(totals.CityTax),
        };
    }

    private static List<RevenueByChannelDto> BuildByChannel(ReportAggregate aggregate, decimal totalRoomNet) =>
        aggregate.ByChannel
            .Select(entry => new RevenueByChannelDto
            {
                Channel = entry.Key.ToString(),
                ReservationCount = entry.Value.ReservationCount,
                SoldRoomNights = entry.Value.SoldRoomNights,
                RoomRevenue = entry.Value.RoomRevenue(),
                ExtraRevenue = entry.Value.ExtraRevenue(),
                CityTaxCollected = ReportMath.Round(entry.Value.CityTax),
                AdrNet = ReportMath.PerUnit(entry.Value.RoomNet, entry.Value.SoldRoomNights),
                RoomRevenueShare = ReportMath.Share(entry.Value.RoomNet, totalRoomNet),
            })
            // Ciroya en cok katkidan aza; esitlikte kanal adi (kararli siralama).
            .OrderByDescending(item => item.RoomRevenue.Net)
            .ThenBy(item => item.Channel, StringComparer.Ordinal)
            .ToList();

    private static List<RevenueByHotelDto> BuildRevenueByHotel(ReportPeriod period, ReportContext context)
    {
        var result = new List<RevenueByHotelDto>(context.Hotels.Count);

        foreach (var hotel in context.Hotels)
        {
            var capacity = context.Capacity.ByHotel[hotel.Id];
            var available = (capacity.RoomCount - capacity.OutOfOrderRoomCount) * period.DayCount;
            var totals = context.Aggregate.ByHotel.TryGetValue(hotel.Id, out var found)
                ? found
                : new ReportTotals();

            result.Add(new RevenueByHotelDto
            {
                HotelId = hotel.Id,
                HotelName = hotel.Name,
                Currency = hotel.Currency,
                SoldRoomNights = totals.SoldRoomNights,
                AvailableRoomNights = available,
                OccupancyRate = ReportMath.Percent(totals.SoldRoomNights, available),
                RoomRevenue = totals.RoomRevenue(),
                ExtraRevenue = totals.ExtraRevenue(),
                TotalRevenue = totals.TotalRevenue(),
                CityTaxCollected = ReportMath.Round(totals.CityTax),
                AdrNet = ReportMath.PerUnit(totals.RoomNet, totals.SoldRoomNights),
                RevParNet = ReportMath.PerUnit(totals.RoomNet, available),
            });
        }

        return result;
    }

    private static List<RevenueDayDto> BuildRevenueDays(
        ReportPeriod period,
        ReportAggregate aggregate,
        int availableRooms)
    {
        var days = new List<RevenueDayDto>(period.DayCount);

        for (var index = 0; index < period.DayCount; index++)
        {
            var day = aggregate.ByDay[index];

            days.Add(new RevenueDayDto
            {
                Date = period.DateAt(index),
                SoldRoomNights = day.SoldRoomNights,
                AvailableRoomNights = availableRooms,
                OccupancyRate = ReportMath.Percent(day.SoldRoomNights, availableRooms),
                RoomRevenue = day.RoomRevenue(),
                ExtraRevenue = day.ExtraRevenue(),
                CityTaxCollected = ReportMath.Round(day.CityTax),
                AdrNet = ReportMath.PerUnit(day.RoomNet, day.SoldRoomNights),
                RevParNet = ReportMath.PerUnit(day.RoomNet, availableRooms),
            });
        }

        return days;
    }

    /// <summary>
    /// <c>RevPAR = ADR × doluluk</c> özdeşliğini <b>iki yoldan</b> hesaplayıp karşılaştırır.
    /// Tanım gereği tutmalıdır; sapma tanımların ayrıştığını gösterir ve uyarı olarak loglanır
    /// (yanıt değiştirilmez — rapor sessizce "düzeltilmiş" bir sayı döndürmez).
    /// </summary>
    private void VerifyRevParIdentity(decimal directRevPar, decimal adr, decimal occupancyRate)
    {
        var derived = ReportMath.Round(adr * occupancyRate / 100m);

        if (Math.Abs(directRevPar - derived) > RevParTolerance)
        {
            logger.RevParMismatch(directRevPar, derived, adr, occupancyRate);
        }
    }

    /// <summary>Kova sözlüğünün anahtarı.</summary>
    private readonly record struct StayBucketKey(
        Guid HotelId,
        DateOnly CheckIn,
        DateOnly CheckOut,
        ReservationChannel Channel);

    /// <summary>Kapsamdaki oda kapasitesi (toplam + otel bazında).</summary>
    private sealed record ReportCapacity(
        int RoomCount,
        int OutOfOrderRoomCount,
        IReadOnlyDictionary<Guid, RoomCapacityRow> ByHotel)
    {
        /// <summary>Satılabilir oda sayısı — servis dışı odalar düşülmüştür (bkz. ReportDefinitions §3).</summary>
        public int AvailableRoomCount => RoomCount - OutOfOrderRoomCount;
    }

    /// <summary>Bir rapor isteğinin yüklenmiş girdileri.</summary>
    private sealed record ReportContext(
        IReadOnlyList<ReportHotelInfo> Hotels,
        ReportScopeDto Scope,
        ReportCapacity Capacity,
        ReportAggregate Aggregate,
        IReadOnlyList<OtherInvoiceRow> OtherInvoiceRows);
}
