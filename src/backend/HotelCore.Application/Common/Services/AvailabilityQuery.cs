using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Common.Services;

/// <summary>
/// Müsaitlik/doluluk sorgularının paylaşılan parçaları. Hem <see cref="AvailabilityService"/>
/// hem doluluk grid'i buradan beslenir; "hangi rezervasyon odayı bloke eder" ve "iki aralık
/// kesişir mi" kararı <b>tek yerde</b> tanımlıdır.
/// <para>
/// Tüm ifadeler <b>SQL'e çevrilebilir</b> biçimde yazılmıştır (bellekte filtreleme yok).
/// </para>
/// </summary>
internal static class AvailabilityQuery
{
    /// <summary>
    /// Odayı bloke ETMEYEN durumlar: <c>Cancelled</c> (iptal) ve <c>NoShow</c> (gelmedi).
    /// Bu iki durumdaki rezervasyon oda takviminden düşer, yani oda tekrar satılabilir.
    /// </summary>
    public static bool IsBlocking(ReservationStatus status) =>
        status is not (ReservationStatus.Cancelled or ReservationStatus.NoShow);

    /// <summary>
    /// Verilen aralıkla kesişen ve odayı bloke eden rezervasyonları süzer.
    /// <para>
    /// <b>Yarı açık aralık <c>[from, to)</c>:</b> <paramref name="from"/> dahil,
    /// <paramref name="to"/> dahil değil. Kesişim koşulu
    /// <c>reservation.CheckIn &lt; to &amp;&amp; from &lt; reservation.CheckOut</c> —
    /// uç noktalarda eşitlik kesişim SAYILMAZ. Böylece bir rezervasyonun çıkış günü, aynı odada
    /// başka bir rezervasyonun giriş günü olabilir (ardışık konaklama).
    /// </para>
    /// </summary>
    /// <param name="reservations">Rezervasyon sorgusu (global query filter uygulanmış).</param>
    /// <param name="from">Aralığın ilk günü (dahil).</param>
    /// <param name="to">Aralığın bitiş günü (dahil değil).</param>
    public static IQueryable<Reservation> BlockingBetween(
        this IQueryable<Reservation> reservations,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(reservations);

        return reservations.Where(reservation =>
            reservation.Status != ReservationStatus.Cancelled
            && reservation.Status != ReservationStatus.NoShow
            && reservation.CheckIn < to
            && from < reservation.CheckOut);
    }
}
