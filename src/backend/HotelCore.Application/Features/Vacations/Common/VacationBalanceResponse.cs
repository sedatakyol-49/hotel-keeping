namespace HotelCore.Application.Features.Vacations.Common;

/// <summary>
/// Yıl bazında izin bakiyesi (Urlaubskonto).
/// <para>
/// Bakiye satırı ilk onayda oluşur. Henüz satırı olmayan çalışan için yanıt
/// <b>türetilmiş</b> bir bakiye döner (<see cref="Id"/> = null, hak = çalışanın
/// <c>annualLeaveDays</c>'i): okuma ucu veri yazmaz, ekran da boş kalmaz.
/// </para>
/// </summary>
public sealed record VacationBalanceResponse
{
    /// <summary>Kayıtlı bakiye satırının kimliği; türetilmiş (henüz yazılmamış) bakiyede null.</summary>
    public Guid? Id { get; init; }

    public Guid EmployeeId { get; init; }

    public string EmployeeName { get; init; } = string.Empty;

    public int Year { get; init; }

    /// <summary>Hak edilen gün (bakiye satırı yoksa çalışanın yıllık izin hakkı).</summary>
    public decimal EntitledDays { get; init; }

    /// <summary>Onaylanmış izinlerle kullanılan gün.</summary>
    public decimal UsedDays { get; init; }

    /// <summary>Önceki yıldan devreden gün.</summary>
    public decimal CarriedOverDays { get; init; }

    /// <summary>
    /// Kalan gün = <c>entitledDays + carriedOverDays − usedDays</c>. Sunucuda hesaplanır ki
    /// "kalan" tanımı istemciler arasında farklılaşmasın.
    /// </summary>
    public decimal RemainingDays { get; init; }
}
