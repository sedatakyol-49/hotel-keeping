using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.RatePlans.Delete;

/// <summary>
/// <c>DELETE /api/v1/rate-plans/{id}</c> — <b>gerçek silme</b> (<c>RatePlan</c> soft-delete
/// edilebilir değildir). Plana bağlı rezervasyon varsa <b>409</b>; bu durumda plan
/// pasifleştirilir (<c>isActive = false</c>).
/// </summary>
public sealed record DeleteRatePlanRequest(Guid Id) : IRequest<Unit>;
