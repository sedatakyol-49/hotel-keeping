using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.Cancel;

/// <summary>
/// <c>POST /api/v1/reservations/{id}/cancel</c> — iptal.
/// <para>
/// <c>CheckedIn</c> ve <c>CheckedOut</c> rezervasyonlar <b>iptal edilemez</b> (409): misafir
/// otele girmiş/çıkmıştır, geçmişi iptalle silmek yerine faturada düzeltme yapılır.
/// Kayıt silinmez; <c>Status = Cancelled</c> olur ve rezervasyon numarasını korur.
/// </para>
/// </summary>
public sealed record CancelReservationRequest : IRequest<ReservationResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>İptal gerekçesi (opsiyonel) — rezervasyon notlarına eklenir.</summary>
    public string? Reason { get; init; }
}
