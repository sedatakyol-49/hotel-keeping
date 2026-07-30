namespace HotelCore.Application.Features.Vacations.Common;

/// <summary>
/// İzin talebi (Urlaubsantrag) — api-contracts.md → "HR (Vacation / TimeTracking / Shifts)".
/// </summary>
public sealed record VacationRequestResponse
{
    public Guid Id { get; init; }

    public Guid EmployeeId { get; init; }

    /// <summary>Görüntüleme için hazır ad; istemcinin ayrıca çalışan çekmesi gerekmez.</summary>
    public string EmployeeName { get; init; } = string.Empty;

    /// <summary>İzin başlangıcı (takvim günü).</summary>
    public DateOnly From { get; init; }

    /// <summary>İzin bitişi — <b>dahil</b>.</summary>
    public DateOnly To { get; init; }

    /// <summary>
    /// Talep edilen gün sayısı. Bu fazda <b>takvim günü</b> olarak hesaplanır
    /// (hafta sonu/resmî tatil düşülmez) — bkz. <see cref="VacationDays"/>.
    /// </summary>
    public decimal RequestedDays { get; init; }

    /// <summary>Durum enum <b>adı</b> (string): <c>Pending | Approved | Rejected | Cancelled</c>.</summary>
    public string Status { get; init; } = string.Empty;

    public string? Reason { get; init; }

    /// <summary>
    /// Kararı veren kullanıcı. Entity alanı <c>VacationRequest.ApprovedByUserId</c>'dir; onay,
    /// ret ve iptal kararlarının hepsinde doldurulur, bu yüzden sözleşmede nötr adla döner.
    /// </summary>
    public Guid? DecidedByUserId { get; init; }

    /// <summary>Kararın verildiği an (UTC) — henüz karar yoksa null.</summary>
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Onay/ret/iptal notu.</summary>
    public string? DecisionNote { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
