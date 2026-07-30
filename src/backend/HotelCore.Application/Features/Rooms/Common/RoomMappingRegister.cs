using Mapster;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda slice'ının Mapster konfigürasyonu (Auth slice'ıyla aynı <c>IRegister</c> deseni).
/// Enum → string dönüşümü tek yerde tanımlanır: sözleşme gereği <c>housekeepingStatus</c>
/// enum ADIYLA taşınır.
/// </summary>
public sealed class RoomMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.NewConfig<RoomRow, RoomResponse>()
            .Map(dest => dest.HousekeepingStatus, src => src.HousekeepingStatus.ToString());

        config.NewConfig<RoomBoardRow, RoomBoardItemDto>()
            .Map(dest => dest.HousekeepingStatus, src => src.HousekeepingStatus.ToString());
    }
}
