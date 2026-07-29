namespace HotelCore.Domain.Enums;

/// <summary>Rezervasyon kanalı — ciro raporlarında kanal dağılımı ve OTA komisyonu için.</summary>
public enum ReservationChannel
{
    Direct = 0,
    Phone = 1,
    WalkIn = 2,
    BookingCom = 3,
    Hrs = 4,
    Expedia = 5,
    Corporate = 6
}
