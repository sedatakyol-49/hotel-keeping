using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.TimeEntries.Common;

/// <summary>
/// Veritabanından okunan ham satır. <c>workedMinutes</c> SQL'de değil satırlar okunduktan sonra
/// hesaplanır: <c>DateTimeOffset</c> farkının dakikaya çevrilmesi sağlayıcıya özgü SQL gerektirir
/// ve yuvarlama davranışını veritabanına bırakırdı.
/// </summary>
/// <param name="Id">Kayıt kimliği.</param>
/// <param name="EmployeeId">Çalışan kimliği.</param>
/// <param name="EmployeeName">Çalışanın görünen adı.</param>
/// <param name="ClockIn">Giriş anı (UTC).</param>
/// <param name="ClockOut">Çıkış anı (UTC) — açık kayıtta null.</param>
/// <param name="BreakMinutes">Mola (dakika).</param>
/// <param name="Source">Kaydın kaynağı.</param>
/// <param name="Note">Not.</param>
internal sealed record TimeEntryRow(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateTimeOffset ClockIn,
    DateTimeOffset? ClockOut,
    int BreakMinutes,
    TimeEntrySource Source,
    string? Note);
