namespace HotelCore.Domain.Enums;

/// <summary>Fatura satırı türü. <see cref="CityTax"/> = Almanya Kurtaxe (kişi × gece × oran).</summary>
public enum InvoiceLineType
{
    RoomCharge = 0,
    Extra = 1,
    CityTax = 2
}
