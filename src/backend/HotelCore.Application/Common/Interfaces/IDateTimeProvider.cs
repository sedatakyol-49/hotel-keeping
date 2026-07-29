namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Test edilebilirlik için zaman soyutlaması. Tüm zaman damgaları UTC saklanır;
/// yerel saate çevirme sunum katmanının işidir.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
