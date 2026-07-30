using HotelCore.Application.Common.Models;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Employees.Common;

/// <summary>
/// Çalışan listesinin normalize edilmiş sorgu parametreleri.
/// </summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama.</param>
/// <param name="DepartmentId">Departman filtresi.</param>
/// <param name="EmploymentType">Çalışma şekli filtresi.</param>
/// <param name="Search">Ad, soyad ve personel numarasında büyük/küçük harf duyarsız arama.</param>
/// <param name="IncludeTerminated">
/// <c>false</c> (varsayılan) ise işten ayrılmışlar listelenmez. Günlük kullanımda beklenen
/// görünüm aktif kadrodur; geçmiş kayıtlar bilinçli olarak istenmelidir.
/// </param>
internal sealed record EmployeeListQuery(
    PageQuery Paging,
    Guid? DepartmentId,
    EmploymentType? EmploymentType,
    string? Search,
    bool IncludeTerminated);
