using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.TimeEntries.Delete;

/// <summary>
/// <c>DELETE /api/v1/time-entries/{id}</c> — hatalı kaydın silinmesi.
/// <para>
/// <c>TimeEntry</c> soft-delete edilebilir değildir (bilinçli): yanlış girilmiş bir mesai
/// kaydının saklanması bir yükümlülük değil, gürültüdür. Fatura kayıtlarının aksine burada
/// saklama zorunluluğu yoktur.
/// </para>
/// </summary>
public sealed record DeleteTimeEntryRequest(Guid Id) : IRequest<Unit>;
