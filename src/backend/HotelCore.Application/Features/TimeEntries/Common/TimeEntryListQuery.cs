using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.TimeEntries.Common;

/// <summary>
/// Zaman kaydı listesinin normalize edilmiş sorgu parametreleri.
/// </summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama.</param>
/// <param name="EmployeeId">Çalışan filtresi.</param>
/// <param name="From">Başlangıç tarihi (dahil) — giriş anı bu günün başından itibaren.</param>
/// <param name="To">Bitiş tarihi (dahil) — giriş anı bu günün sonuna kadar.</param>
internal sealed record TimeEntryListQuery(
    PageQuery Paging,
    Guid? EmployeeId,
    DateOnly? From,
    DateOnly? To);
