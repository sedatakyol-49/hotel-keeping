using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.Create;

/// <summary>Ortak oda tipi kurallarını uygular; Create'e özgü ek kural yoktur.</summary>
public sealed class CreateRoomTypeValidator : RoomTypeWriteValidator<CreateRoomTypeRequest>;
