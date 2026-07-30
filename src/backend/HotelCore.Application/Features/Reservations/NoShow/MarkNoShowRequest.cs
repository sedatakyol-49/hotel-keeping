using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.NoShow;

/// <summary>
/// <c>POST /api/v1/reservations/{id}/no-show</c> — misafir gelmedi.
/// <para>
/// Yalnızca <c>Option</c>/<c>Confirmed</c> durumundan yapılabilir (409). <c>NoShow</c>
/// rezervasyon oda takviminden düşer (çakışma üretmez) ama kayıt ve numara korunur —
/// ceza/komisyon faturalaması için gereklidir.
/// </para>
/// </summary>
public sealed record MarkNoShowRequest(Guid Id) : IRequest<ReservationResponse>;
