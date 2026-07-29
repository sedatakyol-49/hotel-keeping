namespace HotelCore.Domain.Enums;

/// <summary>Kat hizmetleri oda durumu (Odoo housekeeping akışı).</summary>
public enum HousekeepingStatus
{
    Clean = 0,
    Dirty = 1,
    Inspected = 2,
    OutOfOrder = 3
}
