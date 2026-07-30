using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.RoomTypes.Create;

/// <summary>
/// Yeni oda tipi oluşturur. <c>HotelId</c> aktif otelden gelir (elle gövdeden ALINMAZ);
/// kod çakışması 409 döner. Çeviriler entity ile <b>aynı transaction'da</b> yazılır.
/// </summary>
internal sealed class CreateRoomTypeHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    TranslationService translations,
    RoomTypeReader reader)
    : IRequestHandler<CreateRoomTypeRequest, RoomTypeResponse>
{
    public async Task<RoomTypeResponse> Handle(CreateRoomTypeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotelId = currentUser.RequireHotelId();
        var code = request.Code.Trim();

        // Benzersizlik kontrolü aktif otel kapsamındadır (global filter). Nihai güvence
        // (HotelId, Code) unique index'idir; yarış durumunda index devreye girer.
        var codeExists = await database.RoomTypes
            .AnyAsync(roomType => roomType.Code == code, cancellationToken)
            .ConfigureAwait(false);

        if (codeExists)
        {
            throw new ConflictException($"'{code}' kodlu oda tipi bu otelde zaten mevcut.");
        }

        var entity = new RoomType
        {
            HotelId = hotelId,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            BasePrice = request.BasePrice,
            Capacity = request.Capacity,
            SizeSqm = request.SizeSqm,
            Amenities = AmenityList.Format(request.Amenities)
        };

        database.RoomTypes.Add(entity);

        // Id uygulama tarafında üretildiği için çeviriler kayıttan ÖNCE eklenebilir (tek SaveChanges).
        await translations
            .UpsertAsync(
                TranslationEntityTypes.RoomType,
                entity.Id,
                RoomTypeTranslationInput.ToFieldValues(request.Translations),
                cancellationToken)
            .ConfigureAwait(false);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entity.Id, includeTranslations: true, cancellationToken).ConfigureAwait(false);
    }
}
