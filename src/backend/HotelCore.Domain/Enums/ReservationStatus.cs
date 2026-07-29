namespace HotelCore.Domain.Enums;

/// <summary>Rezervasyon yaşam döngüsü: Option → Confirmed → CheckedIn → CheckedOut.</summary>
public enum ReservationStatus
{
    /// <summary>Opsiyon — henüz kesinleşmemiş (grid'de kesikli çizgi).</summary>
    Option = 0,
    Confirmed = 1,
    CheckedIn = 2,
    CheckedOut = 3,
    Cancelled = 4,
    NoShow = 5
}
