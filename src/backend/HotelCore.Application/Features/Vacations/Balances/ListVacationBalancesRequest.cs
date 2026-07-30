using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Balances;

/// <summary>
/// <c>GET /api/v1/vacations/balances?employeeId=&amp;year=</c> — aktif otelin izin bakiyeleri.
/// <para>
/// Düz dizi döner (sayfalama yok): kapsam tek bir yıl ve tek bir otelin kadrosudur.
/// <c>year</c> verilmezse sunucunun geçerli yılı kullanılır; <c>employeeId</c> verilirse
/// yalnızca o çalışanın bakiyesi döner (çalışan yoksa 404).
/// </para>
/// </summary>
public sealed record ListVacationBalancesRequest : IRequest<IReadOnlyList<VacationBalanceResponse>>
{
    public Guid? EmployeeId { get; init; }

    /// <summary>Takvim yılı; boş bırakılırsa geçerli yıl.</summary>
    public int? Year { get; init; }
}
