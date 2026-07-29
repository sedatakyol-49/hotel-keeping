using HotelCore.Domain.Entities;
using Mapster;

namespace HotelCore.Application.Features.Auth.Common;

/// <summary>
/// Auth slice'ının Mapster konfigürasyonu. <c>IRegister</c> implementasyonları
/// <c>AddApplication()</c> içindeki assembly taramasıyla otomatik bulunur.
/// </summary>
public sealed class AuthMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Country bir enum; frontend ISO kodu metni bekler ("DE").
        config.NewConfig<Hotel, HotelSummaryDto>()
            .Map(dest => dest.Country, src => src.Country.ToString());
    }
}
