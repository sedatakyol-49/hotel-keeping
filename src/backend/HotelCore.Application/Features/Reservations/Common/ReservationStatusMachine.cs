using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyon durum makinesi — <b>tüm</b> geçiş kuralları burada, tek yerde tanımlıdır
/// (architecture.md §4.3: <c>Option → Confirmed → CheckedIn → CheckedOut</c>, yan dallar
/// <c>Cancelled</c> / <c>NoShow</c>).
/// <para>
/// Her use-case (check-in / check-out / cancel / no-show / confirm) yalnızca
/// <see cref="EnsureCanTransition"/> çağırır; "hangi durumdan hangi duruma geçilebilir"
/// bilgisi handler'lara dağılmaz. Geçersiz geçiş <b>409</b> döner ve mesaj <b>hangi geçişin
/// denendiğini</b> ve izin verilen hedefleri söyler.
/// </para>
/// </summary>
internal static class ReservationStatusMachine
{
    /// <summary>
    /// İzin verilen geçişler. Terminal durumlar (<c>CheckedOut</c>, <c>Cancelled</c>,
    /// <c>NoShow</c>) bilinçli olarak boştur: geçmişi "geri almak" yeni bir rezervasyon
    /// (veya faturada Stornorechnung) ile yapılır, durum geri çevrilerek yapılmaz.
    /// </summary>
    private static readonly Dictionary<ReservationStatus, ReservationStatus[]> AllowedTransitions = new()
    {
        [ReservationStatus.Option] =
        [
            ReservationStatus.Confirmed,
            ReservationStatus.CheckedIn,
            ReservationStatus.Cancelled,
            ReservationStatus.NoShow
        ],
        [ReservationStatus.Confirmed] =
        [
            ReservationStatus.CheckedIn,
            ReservationStatus.Cancelled,
            ReservationStatus.NoShow
        ],
        [ReservationStatus.CheckedIn] = [ReservationStatus.CheckedOut],
        [ReservationStatus.CheckedOut] = [],
        [ReservationStatus.Cancelled] = [],
        [ReservationStatus.NoShow] = [],
    };

    /// <summary>
    /// Geçişi doğrular; geçersizse <see cref="ConflictException"/> (409) fırlatır.
    /// </summary>
    /// <param name="from">Mevcut durum.</param>
    /// <param name="to">Hedef durum.</param>
    public static void EnsureCanTransition(ReservationStatus from, ReservationStatus to)
    {
        if (from == to)
        {
            throw new ConflictException(Messages.ReservationSameStatus(from));
        }

        var allowed = AllowedTransitions.TryGetValue(from, out var targets) ? targets : [];

        if (allowed.Contains(to))
        {
            return;
        }

        throw new ConflictException(Messages.ReservationInvalidTransition(from, to, allowed));
    }

    /// <summary>
    /// Tarih/oda/kişi değişikliğine (PUT) izin verilen durumlar: <c>Option</c>, <c>Confirmed</c>
    /// ve <c>CheckedIn</c> (konaklama uzatma/oda değişikliği meşrudur). Nihai durumlarda
    /// (<c>CheckedOut</c>, <c>Cancelled</c>, <c>NoShow</c>) kayıt dondurulur → 409.
    /// </summary>
    /// <param name="status">Rezervasyonun mevcut durumu.</param>
    public static void EnsureModifiable(ReservationStatus status)
    {
        if (status is ReservationStatus.Option
            or ReservationStatus.Confirmed
            or ReservationStatus.CheckedIn)
        {
            return;
        }

        throw new ConflictException(Messages.ReservationNotModifiable(status));
    }
}
