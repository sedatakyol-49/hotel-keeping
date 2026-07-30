using HotelCore.Application.Common.Models;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>Rezervasyon listesinin normalize edilmiş sorgu parametreleri.</summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama.</param>
/// <param name="Status">Durum filtresi.</param>
/// <param name="Channel">Kanal filtresi.</param>
/// <param name="RoomId">Oda filtresi.</param>
/// <param name="GuestId">Misafir filtresi.</param>
/// <param name="From">
/// Tarih aralığı başlangıcı — konaklaması bu günden <b>sonra biten</b> rezervasyonlar
/// (<c>CheckOut &gt; from</c>).
/// </param>
/// <param name="To">
/// Tarih aralığı bitişi (dahil değil) — konaklaması bu günden <b>önce başlayan</b>
/// rezervasyonlar (<c>CheckIn &lt; to</c>). <c>From</c> ile birlikte verildiğinde sonuç,
/// aralıkla <b>kesişen</b> konaklamalardır (yarı açık aralık mantığı).
/// </param>
/// <param name="Search">Rezervasyon numarası veya misafir ad/soyadında "contains" arama.</param>
internal sealed record ReservationListQuery(
    PageQuery Paging,
    ReservationStatus? Status,
    ReservationChannel? Channel,
    Guid? RoomId,
    Guid? GuestId,
    DateOnly? From,
    DateOnly? To,
    string? Search);
