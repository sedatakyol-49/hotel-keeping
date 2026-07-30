namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda Create/Update isteklerinin paylaştığı gövde sözleşmesi; doğrulama kuralları
/// <see cref="RoomWriteValidator{T}"/> içinde tek yerde tanımlanır.
/// </summary>
public interface IRoomWriteRequest
{
    string Number { get; }

    int Floor { get; }

    Guid RoomTypeId { get; }

    string? Note { get; }
}
