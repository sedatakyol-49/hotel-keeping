using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.RoomTypes.Update;

/// <summary>
/// Oda tipini günceller. Kod çakışması 409, bulunamayan kayıt 404 döner. Çeviriler upsert
/// edilir (bkz. <see cref="RoomTypeTranslationInput"/>).
/// </summary>
internal sealed class UpdateRoomTypeHandler(
    IAppDbContext database,
    TranslationService translations,
    RoomTypeReader reader)
    : IRequestHandler<UpdateRoomTypeRequest, RoomTypeResponse>
{
    public async Task<RoomTypeResponse> Handle(UpdateRoomTypeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await database.RoomTypes
            .FirstOrDefaultAsync(roomType => roomType.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(RoomType), request.Id);

        var code = request.Code.Trim();

        // Not: buradaki HotelId koşulu tenant filtresi DEĞİL, benzersizlik kapsamıdır —
        // unique index (HotelId, Code) olduğu için çakışma kaydın kendi oteli içinde aranır
        // (Head Office konsolide modunda filtre bypass edildiğinde de doğru sonuç verir).
        var codeExists = await database.RoomTypes
            .AnyAsync(
                roomType => roomType.Id != entity.Id
                            && roomType.HotelId == entity.HotelId
                            && roomType.Code == code,
                cancellationToken)
            .ConfigureAwait(false);

        if (codeExists)
        {
            throw new ConflictException(Messages.RoomTypeCodeTaken(code));
        }

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.BasePrice = request.BasePrice;
        entity.Capacity = request.Capacity;
        entity.SizeSqm = request.SizeSqm;
        entity.Amenities = AmenityList.Format(request.Amenities);

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
