using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Aynı <c>(otel, giriş, çıkış, kanal)</c> anahtarına sahip konaklamaların birleşik kovası:
/// SQL'den gelen rezervasyon toplamları ile fatura toplamları burada buluşur.
/// </summary>
internal sealed class StayBucket(Guid hotelId, DateOnly checkIn, DateOnly checkOut, ReservationChannel channel)
{
    public Guid HotelId { get; } = hotelId;

    public DateOnly CheckIn { get; } = checkIn;

    public DateOnly CheckOut { get; } = checkOut;

    public ReservationChannel Channel { get; } = channel;

    /// <summary>Konaklamanın toplam gece sayısı (yarı açık aralık).</summary>
    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;

    public int ReservationCount { get; set; }

    /// <summary><c>Reservation.TotalAmount</c> toplamı (brüt konaklama tutarı).</summary>
    public decimal ReservationAmount { get; set; }

    /// <summary>En az bir kez kesinleşmiş faturası olan rezervasyonların <c>TotalAmount</c> toplamı.</summary>
    public decimal BilledReservationAmount { get; set; }

    public decimal RoomNet { get; set; }

    public decimal RoomVat { get; set; }

    public decimal ExtraNet { get; set; }

    public decimal ExtraVat { get; set; }

    /// <summary>Kurtaxe (KDV'siz kalem) — gelir değildir.</summary>
    public decimal CityTax { get; set; }
}

/// <summary>Bir kırılımın (toplam / gün / kanal / otel) biriken değerleri — <b>yuvarlanmamış</b>.</summary>
internal sealed class ReportTotals
{
    public int SoldRoomNights { get; set; }

    public int ReservationCount { get; set; }

    public decimal RoomNet { get; set; }

    public decimal RoomVat { get; set; }

    public decimal ExtraNet { get; set; }

    public decimal ExtraVat { get; set; }

    public decimal CityTax { get; set; }

    public decimal UnbilledGross { get; set; }

    public decimal RoomGross => RoomNet + RoomVat;

    public decimal ExtraGross => ExtraNet + ExtraVat;

    public RevenueAmountsDto RoomRevenue() => Amounts(RoomNet, RoomVat);

    public RevenueAmountsDto ExtraRevenue() => Amounts(ExtraNet, ExtraVat);

    public RevenueAmountsDto TotalRevenue() => Amounts(RoomNet + ExtraNet, RoomVat + ExtraVat);

    /// <summary>
    /// <c>net + vat == gross</c> değişmezi korunur: brüt <b>yuvarlanmış</b> net ve KDV'nin
    /// toplamıdır, ayrıca yuvarlanmaz (fatura modülüyle aynı yaklaşım).
    /// </summary>
    private static RevenueAmountsDto Amounts(decimal net, decimal vat)
    {
        var roundedNet = ReportMath.Round(net);
        var roundedVat = ReportMath.Round(vat);

        return new RevenueAmountsDto
        {
            Net = roundedNet,
            Vat = roundedVat,
            Gross = roundedNet + roundedVat,
        };
    }
}

/// <summary>Kova toplamlarının gün / kanal / otel eksenlerine yayılmış hâli.</summary>
internal sealed class ReportAggregate
{
    public ReportTotals Total { get; } = new();

    /// <summary>Gün başına bir eleman (<c>ReportPeriod.DayCount</c> uzunluğunda).</summary>
    public IReadOnlyList<ReportTotals> ByDay { get; init; } = [];

    public Dictionary<ReservationChannel, ReportTotals> ByChannel { get; } = [];

    public Dictionary<Guid, ReportTotals> ByHotel { get; } = [];
}

/// <summary>
/// Kova toplamlarını gün eksenine yayan çekirdek. <b>Tek geçiş</b>, sorgu yok.
///
/// <para><b>Dağıtım kuralı (Periodenabgrenzung):</b> bir konaklamanın geliri gecelerine
/// <b>eşit</b> dağıtılır (<c>gelir / gece sayısı</c>) ve rapor penceresine düşen geceler kadarı
/// sayılır. Gerekçe <see cref="RevenueRecognition"/>'da: fatura satırı konaklamanın tamamı için
/// tek satırdır; belge tarihine göre atıf yapılsaydı çok geceli konaklamanın tüm geliri giriş
/// gününe düşer ve <c>ADR = gelir / oda-gece</c> anlamını yitirirdi.</para>
///
/// <para><b>Yuvarlama:</b> toplamlar <c>tutar × penceredeki gece / toplam gece</c> ile <b>tek</b>
/// bölme kullanılarak biriktirilir; günlük seri ise <c>tutar / toplam gece</c> ile beslenir.
/// İkisi matematiksel olarak aynıdır, yalnızca <c>decimal</c>'in son basamaklarında ayrışabilir —
/// 2 haneye yuvarlandıktan sonra fark görünmez.</para>
/// </summary>
internal static class ReportAggregator
{
    public static ReportAggregate Build(ReportPeriod period, IEnumerable<StayBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(buckets);

        var byDay = new ReportTotals[period.DayCount];
        for (var index = 0; index < byDay.Length; index++)
        {
            byDay[index] = new ReportTotals();
        }

        var aggregate = new ReportAggregate { ByDay = byDay };

        foreach (var bucket in buckets)
        {
            var nights = bucket.Nights;
            if (nights <= 0)
            {
                // Savunma: 0 geceli konaklama rezervasyon modülünde engellenir (checkOut > checkIn).
                continue;
            }

            var clip = period.Clip(bucket.CheckIn, bucket.CheckOut);
            if (clip.Length == 0)
            {
                continue;
            }

            var slice = new BucketSlice(bucket, nights, clip.Length);

            Accumulate(aggregate.Total, slice);
            Accumulate(GetOrAdd(aggregate.ByChannel, bucket.Channel), slice);
            Accumulate(GetOrAdd(aggregate.ByHotel, bucket.HotelId), slice);

            for (var offset = 0; offset < clip.Length; offset++)
            {
                AccumulateNight(byDay[clip.StartIndex + offset], bucket, nights);
            }
        }

        return aggregate;
    }

    private static TTotals GetOrAdd<TKey, TTotals>(Dictionary<TKey, TTotals> map, TKey key)
        where TKey : notnull
        where TTotals : new()
    {
        if (!map.TryGetValue(key, out var totals))
        {
            totals = new TTotals();
            map[key] = totals;
        }

        return totals;
    }

    /// <summary>Kovanın pencereye düşen payını bir kırılıma ekler.</summary>
    private static void Accumulate(ReportTotals target, BucketSlice slice)
    {
        var bucket = slice.Bucket;

        target.SoldRoomNights += bucket.ReservationCount * slice.NightsInRange;

        // Rezervasyon sayisi konaklama basina BIR KEZ sayilir (gece basina degil):
        // "kac rezervasyon bu donemle kesisti" sorusunun cevabi.
        target.ReservationCount += bucket.ReservationCount;

        target.RoomNet += slice.Share(bucket.RoomNet);
        target.RoomVat += slice.Share(bucket.RoomVat);
        target.ExtraNet += slice.Share(bucket.ExtraNet);
        target.ExtraVat += slice.Share(bucket.ExtraVat);
        target.CityTax += slice.Share(bucket.CityTax);
        target.UnbilledGross += slice.Share(bucket.ReservationAmount - bucket.BilledReservationAmount);
    }

    /// <summary>Kovanın <b>tek</b> gecesini günlük seriye ekler.</summary>
    private static void AccumulateNight(ReportTotals target, StayBucket bucket, int nights)
    {
        target.SoldRoomNights += bucket.ReservationCount;
        target.RoomNet += bucket.RoomNet / nights;
        target.RoomVat += bucket.RoomVat / nights;
        target.ExtraNet += bucket.ExtraNet / nights;
        target.ExtraVat += bucket.ExtraVat / nights;
        target.CityTax += bucket.CityTax / nights;
        target.UnbilledGross += (bucket.ReservationAmount - bucket.BilledReservationAmount) / nights;
    }

    /// <summary>Kovanın pencereye düşen oranı: <c>penceredeki gece / toplam gece</c>.</summary>
    private readonly record struct BucketSlice(StayBucket Bucket, int TotalNights, int NightsInRange)
    {
        public decimal Share(decimal amount) =>
            NightsInRange == TotalNights ? amount : amount * NightsInRange / TotalNights;
    }
}
