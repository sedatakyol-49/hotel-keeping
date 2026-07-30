using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.GetById;

/// <summary>
/// Tek oda tipini döner. Başka otelin kaydı global query filter yüzünden görünmez ve
/// <c>NotFoundException</c> (404) ile sonuçlanır — varlığın var olduğu bilgisi sızdırılmaz.
/// </summary>
internal sealed class GetRoomTypeByIdHandler(RoomTypeReader reader)
    : IRequestHandler<GetRoomTypeByIdRequest, RoomTypeResponse>
{
    public Task<RoomTypeResponse> Handle(GetRoomTypeByIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, includeTranslations: true, cancellationToken);
    }
}
