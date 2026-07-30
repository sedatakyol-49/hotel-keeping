using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyon sorgusunun düz izdüşümü (navigasyonlar materyalize edilmez; yalnızca gereken
/// kolonlar okunur). Enum'lar ham tutulur, string'e çevirme ve aritmetik (gece sayısı, ön ödeme
/// tutarı) C# tarafında yapılır — <c>DateOnly</c> aritmetiğinin SQL çevirisine bağımlı kalmamak
/// için bilinçli bir tercih.
/// </summary>
/// <param name="Id">Rezervasyon kimliği.</param>
/// <param name="ReservationNumber">Okunur kod.</param>
/// <param name="Status">Durum.</param>
/// <param name="Channel">Kanal.</param>
/// <param name="RoomId">Oda kimliği.</param>
/// <param name="RoomNumber">Oda numarası.</param>
/// <param name="RoomTypeId">Oda tipi kimliği.</param>
/// <param name="RoomTypeCode">Oda tipi kodu.</param>
/// <param name="GuestId">Misafir kimliği.</param>
/// <param name="GuestFirstName">Misafir adı.</param>
/// <param name="GuestLastName">Misafir soyadı.</param>
/// <param name="GuestEmail">Misafir e-postası.</param>
/// <param name="CheckIn">Giriş günü (dahil).</param>
/// <param name="CheckOut">Çıkış günü (dahil değil).</param>
/// <param name="Adults">Yetişkin sayısı.</param>
/// <param name="Children">Çocuk sayısı.</param>
/// <param name="TotalAmount">Toplam brüt tutar.</param>
/// <param name="Currency">Otelin para birimi.</param>
/// <param name="DepositPercent">Ön ödeme yüzdesi.</param>
/// <param name="RatePlanId">Fiyat planı kimliği.</param>
/// <param name="RatePlanName">Fiyat planı adı.</param>
/// <param name="Notes">Serbest not.</param>
/// <param name="CheckedInAt">Check-in zamanı.</param>
/// <param name="CheckedOutAt">Check-out zamanı.</param>
/// <param name="FolioId">Açık hesap kimliği.</param>
internal sealed record ReservationRow(
    Guid Id,
    string ReservationNumber,
    ReservationStatus Status,
    ReservationChannel Channel,
    Guid RoomId,
    string RoomNumber,
    Guid RoomTypeId,
    string RoomTypeCode,
    Guid GuestId,
    string GuestFirstName,
    string GuestLastName,
    string? GuestEmail,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    decimal TotalAmount,
    string Currency,
    decimal DepositPercent,
    Guid? RatePlanId,
    string? RatePlanName,
    string? Notes,
    DateTimeOffset? CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    Guid? FolioId);
