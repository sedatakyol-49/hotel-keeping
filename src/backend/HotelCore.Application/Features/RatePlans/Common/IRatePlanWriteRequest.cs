using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.RatePlans.Common;

/// <summary>Create ve Update isteklerinin paylaştığı gövde sözleşmesi.</summary>
public interface IRatePlanWriteRequest
{
    Guid RoomTypeId { get; }

    string Name { get; }

    decimal Price { get; }

    DateOnly ValidFrom { get; }

    DateOnly ValidTo { get; }

    /// <summary><c>null</c> ise plan tüm kanallar için geçerlidir.</summary>
    ReservationChannel? Channel { get; }

    /// <summary>Opsiyonel; verilmezse <c>true</c>.</summary>
    bool? IsActive { get; }
}
