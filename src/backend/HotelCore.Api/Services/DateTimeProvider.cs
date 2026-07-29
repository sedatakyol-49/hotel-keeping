using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Api.Services;

/// <summary>
/// Sistem saatini döndüren üretim implementasyonu. Testlerde sahte bir sağlayıcı ile
/// değiştirilebilmesi için tüm zaman okumaları bu port üzerinden yapılır.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
