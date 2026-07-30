using Mapster;

namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Oda tipi slice'ının Mapster konfigürasyonu. <c>IRegister</c> implementasyonları
/// <c>AddApplication()</c> içindeki assembly taramasıyla otomatik bulunur (Auth slice'ıyla aynı desen).
/// </summary>
public sealed class RoomTypeMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Amenities: DB'de virgüllü metin → API'de dizi. Dönüşüm tek yerde (AmenityList).
        // Translations ve çözümlenmiş Name/Description sorgu sonrası (reader'da) doldurulur.
        config.NewConfig<RoomTypeRow, RoomTypeResponse>()
            .Map(dest => dest.Amenities, src => AmenityList.Parse(src.Amenities));
    }
}
