using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Shifts.Common;

namespace HotelCore.Application.Features.Shifts.GetPlan;

/// <summary>
/// <c>GET /api/v1/shifts?week=YYYY-Www</c> <b>veya</b> <c>?from=&amp;to=</c> — vardiya planı.
/// <para>
/// İkisi birlikte gönderilirse <b><c>week</c> kazanır</b> ve <c>from/to</c> yok sayılır
/// (bkz. <see cref="ShiftPlanRange.Resolve"/>). Hiçbiri gönderilmezse geçerli ISO hafta döner.
/// Kullanılan aralık yanıtta (<c>from</c>, <c>to</c>, <c>week</c>) geri bildirilir.
/// </para>
/// </summary>
public sealed record GetShiftPlanRequest : IRequest<ShiftPlanResponse>
{
    /// <summary>ISO 8601 hafta etiketi, örn. <c>2026-W31</c> (hafta Pazartesi başlar).</summary>
    public string? Week { get; init; }

    /// <summary>Serbest aralık başlangıcı (dahil) — <c>to</c> ile birlikte verilmelidir.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Serbest aralık bitişi (dahil) — <c>from</c> ile birlikte verilmelidir.</summary>
    public DateOnly? To { get; init; }
}
