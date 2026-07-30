using HotelCore.Application.Common.Models;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Vacations.Common;

/// <summary>
/// İzin listesinin normalize edilmiş sorgu parametreleri.
/// </summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama.</param>
/// <param name="EmployeeId">Çalışan filtresi.</param>
/// <param name="Status">Durum filtresi.</param>
/// <param name="Year">Takvim yılı filtresi — talep aralığı o yılla <b>kesişiyorsa</b> listelenir.</param>
/// <param name="From">Aralık başlangıcı — talebin bitişi bu tarihten önce olan kayıtlar düşer.</param>
/// <param name="To">Aralık bitişi — talebin başlangıcı bu tarihten sonra olan kayıtlar düşer.</param>
internal sealed record VacationListQuery(
    PageQuery Paging,
    Guid? EmployeeId,
    VacationStatus? Status,
    int? Year,
    DateOnly? From,
    DateOnly? To);
