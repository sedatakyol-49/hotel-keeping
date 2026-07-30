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

    private static readonly Action<ILogger, string, string, Exception?> ExclusionConstraintViolationMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4001, "ExclusionConstraintViolation"),
            "Dislama (EXCLUDE) kisiti ihlali 409 Conflict'e cevrildi. Kisit: {ConstraintName}, tablo: {TableName}");

    /// <summary>
    /// Aralık çakışması kısıtı (örn. fiyat planı <c>EXCLUDE USING gist</c>) ihlali. Bu uyarının
    /// görülmesi, handler'ın ön kontrolünü atlatan <b>eşzamanlı</b> bir yazma olduğunu gösterir —
    /// yani kısıt tam olarak var olma sebebini yerine getirmiştir.
    /// </summary>
    public static void ExclusionConstraintViolation(
        this ILogger logger,
        string constraintName,
        string tableName,
        Exception exception) =>
        ExclusionConstraintViolationMessage(logger, constraintName, tableName, exception);
}
