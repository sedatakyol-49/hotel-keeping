using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.Guests.Common;

/// <summary>Misafir listesinin normalize edilmiş sorgu parametreleri.</summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama.</param>
/// <param name="Search">Ad, soyad ve e-postada büyük/küçük harf duyarsız "contains" arama.</param>
internal sealed record GuestListQuery(PageQuery Paging, string? Search);
