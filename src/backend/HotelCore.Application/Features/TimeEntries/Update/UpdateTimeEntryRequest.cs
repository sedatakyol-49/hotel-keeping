using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.Update;

/// <summary>
/// <c>PUT /api/v1/time-entries/{id}</c> gövdesi — manuel düzeltme (Korrektur).
/// <para>
/// Çalışan <b>değiştirilemez</b>: bir kaydın sahibini değiştirmek iki çalışanın mesaisini
/// karıştırır; yanlış çalışana yazılan kayıt silinip yenisi oluşturulmalıdır.
/// </para>
/// </summary>
public sealed record UpdateTimeEntryRequest : IRequest<TimeEntryResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>Giriş anı (zorunlu); gelecekte olamaz.</summary>
    public DateTimeOffset ClockIn { get; init; }

    /// <summary>
    /// Çıkış anı; <c>null</c> gönderilirse kayıt yeniden "açık" hâle gelir — bu durumda
    /// çalışanın başka açık kaydı olmamalıdır (aksi hâlde 409).
    /// </summary>
    public DateTimeOffset? ClockOut { get; init; }

    public int BreakMinutes { get; init; }

    public string? Note { get; init; }
}
