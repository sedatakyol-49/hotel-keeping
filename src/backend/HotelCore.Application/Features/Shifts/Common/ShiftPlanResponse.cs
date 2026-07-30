namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>Plan ızgarasının satır ekseni: çalışan.</summary>
/// <param name="Id">Çalışan kimliği.</param>
/// <param name="FullName">Görünen ad (soyad, ad sırasında sıralanır).</param>
/// <param name="DepartmentName">Departman adı — ızgarada gruplama/renklendirme için.</param>
public sealed record ShiftPlanEmployeeDto(Guid Id, string FullName, string DepartmentName);

/// <summary>
/// Plan ızgarasının sütun ekseni: gün. Aralıktaki <b>her</b> gün döner; vardiyası olmayan günde
/// <see cref="Shifts"/> boş dizidir (istemcinin eksik günleri kendisi üretmesi gerekmez).
/// </summary>
/// <param name="Date">Gün.</param>
/// <param name="Shifts">O güne planlanmış vardiyalar (çalışan adına göre sıralı).</param>
public sealed record ShiftPlanDayDto(DateOnly Date, IReadOnlyList<ShiftResponse> Shifts);

/// <summary>
/// <c>GET /api/v1/shifts</c> yanıtı: gün × çalışan ızgarası.
/// <para>
/// Yanıt <b>gün bazında gruplanır</b> ve satır ekseni ayrı bir <see cref="Employees"/> listesiyle
/// verilir: haftalık plan ekranı sütun = gün, satır = çalışan şeklinde çizilir; vardiyası olmayan
/// çalışan da ızgarada boş satır olarak görünmelidir. Aralık ve hafta etiketi yanıtta geri döner
/// ki istemci hangi dönemi gördüğünü doğrulayabilsin.
/// </para>
/// </summary>
public sealed record ShiftPlanResponse
{
    /// <summary>Aralık başlangıcı (dahil).</summary>
    public DateOnly From { get; init; }

    /// <summary>Aralık bitişi (dahil).</summary>
    public DateOnly To { get; init; }

    /// <summary>
    /// ISO hafta etiketi (<c>2026-W31</c>). <c>week</c> parametresi kullanıldığında veya
    /// parametresiz (geçerli hafta) istekte dolu; serbest <c>from/to</c> aralığında null.
    /// </summary>
    public string? Week { get; init; }

    public IReadOnlyList<ShiftPlanDayDto> Days { get; init; } = [];

    public IReadOnlyList<ShiftPlanEmployeeDto> Employees { get; init; } = [];
}
