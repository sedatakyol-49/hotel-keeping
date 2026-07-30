namespace HotelCore.Application.Features.Employees.Common;

/// <summary>
/// Çalışan — api-contracts.md → "Personel (Employees &amp; Departments)" ile birebir.
/// </summary>
public sealed record EmployeeResponse
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    /// <summary>Görüntüleme için hazır ad — istemcinin birleştirmesi gerekmez.</summary>
    public string FullName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Personel numarası (Personalnummer) — otel içinde benzersiz.</summary>
    public string? StaffNumber { get; init; }

    public Guid DepartmentId { get; init; }

    public string DepartmentName { get; init; } = string.Empty;

    /// <summary>Çalışma şekli enum <b>adı</b> (string), sayı değil.</summary>
    public string EmploymentType { get; init; } = string.Empty;

    public decimal AnnualLeaveDays { get; init; }

    public DateOnly HiredOn { get; init; }

    public DateOnly? TerminatedOn { get; init; }

    /// <summary>
    /// Halen çalışıyor mu: ayrılış tarihi yok ya da gelecekte. Sunucuda hesaplanır ki
    /// "aktif" tanımı istemciler arasında farklılaşmasın.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>Opsiyonel login ilişkisi — her çalışanın sisteme girişi olmayabilir.</summary>
    public Guid? UserId { get; init; }
}
