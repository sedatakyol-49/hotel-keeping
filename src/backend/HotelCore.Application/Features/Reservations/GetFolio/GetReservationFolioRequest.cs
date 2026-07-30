using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.GetFolio;

/// <summary>
/// <c>GET /api/v1/reservations/{id}/folio</c> — detay çekmecesi için açık hesap satırları
/// ve toplamları. Fatura henüz yoktur; folio açık hesap olarak durur.
/// </summary>
public sealed record GetReservationFolioRequest(Guid Id) : IRequest<FolioResponse>;
