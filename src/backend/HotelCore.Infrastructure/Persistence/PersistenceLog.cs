using Microsoft.Extensions.Logging;

namespace HotelCore.Infrastructure.Persistence;

/// <summary>Persistence katmanının log mesajları (LoggerMessage delegeleri — CA1848).</summary>
internal static class PersistenceLog
{
    private static readonly Action<ILogger, string, string, Exception?> UniqueConstraintViolationMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4000, "UniqueConstraintViolation"),
            "Benzersizlik kisiti ihlali 409 Conflict'e cevrildi. Kisit: {ConstraintName}, tablo: {TableName}");

    /// <summary>
    /// Kısıt adı yalnızca sunucu log'una yazılır; istemciye şema detayı sızdırılmaz.
    /// Aynı kısıt için tekrarlayan uyarılar ön kontrolü atlatan yarış durumlarını (veya eksik
    /// bir ön kontrolü) işaret eder.
    /// </summary>
    public static void UniqueConstraintViolation(
        this ILogger logger,
        string constraintName,
        string tableName,
        Exception exception) =>
        UniqueConstraintViolationMessage(logger, constraintName, tableName, exception);
}
