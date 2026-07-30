using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Create ve Update isteklerinin paylaştığı gövde sözleşmesi.
/// <para>
/// <b>Tutar bilinçli olarak YOKTUR:</b> <c>totalAmount</c> istemciden alınmaz, sunucuda
/// hesaplanır (bkz. <c>ReservationPricingService</c>).
/// </para>
/// </summary>
public interface IReservationWriteRequest
{
    Guid RoomId { get; }

    Guid GuestId { get; }

    DateOnly CheckIn { get; }

    /// <summary>Çıkış günü — <c>CheckIn</c>'den sonra olmalıdır (yarı açık aralık).</summary>
    DateOnly CheckOut { get; }

    int Adults { get; }

    int Children { get; }

    ReservationChannel Channel { get; }

    /// <summary>Ön ödeme yüzdesi (0–100).</summary>
    decimal DepositPercent { get; }

    string? Notes { get; }
}
