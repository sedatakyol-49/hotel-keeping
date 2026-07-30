using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.List;

/// <summary>
/// Aktif otelin oda tiplerini döner (Head Office konsolide modunda tüm oteller).
/// Ad/açıklama aktif dile göre çözümlenir; <c>translations</c> alanı liste yanıtında bulunmaz.
/// </summary>
internal sealed class ListRoomTypesHandler(RoomTypeReader reader)
    : IRequestHandler<ListRoomTypesRequest, IReadOnlyList<RoomTypeResponse>>
{
    public Task<IReadOnlyList<RoomTypeResponse>> Handle(
        ListRoomTypesRequest request,
        CancellationToken cancellationToken) =>
        reader.ListAsync(cancellationToken);
}
