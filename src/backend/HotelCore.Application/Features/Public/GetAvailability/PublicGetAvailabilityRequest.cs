using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetAvailability;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}/availability</c> — arama + fiyat teklifi.
/// <para>
/// <b>Hold OLUŞTURMAZ.</b> Salt okuma; sayfa yenilendiğinde tekrar çağrılabilir ve envanteri
/// park etmez. Envanter ancak misafir bir teklifi seçtiğinde (<c>POST /holds</c>) tutulur.
/// </para>
/// </summary>
public sealed record PublicGetAvailabilityRequest : IRequest<PublicAvailabilityResponse>
{
    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Adults { get; init; } = 2;

    public int Children { get; init; }
}
