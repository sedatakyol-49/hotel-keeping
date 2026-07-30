using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Guests.Delete;

/// <summary>
/// <c>DELETE /api/v1/guests/{id}</c> — soft-delete. Aktif veya gelecek tarihli rezervasyonu olan
/// misafir silinemez (<b>409</b>); geçmiş konaklamalar engel değildir ve tarihçe korunur.
/// </summary>
public sealed record DeleteGuestRequest(Guid Id) : IRequest<Unit>;
