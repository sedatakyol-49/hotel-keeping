namespace HotelCore.Api.Startup;

/// <summary>
/// Api katmanının log mesajları. <see cref="LoggerMessage"/> delegeleri kullanılır:
/// şablon bir kez derlenir, seviye kapalıysa argümanlar değerlendirilmez (CA1848/CA1873).
/// </summary>
internal static class ApiLog
{
    private static readonly Action<ILogger, string, string, int, Exception?> UnhandledExceptionMessage =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Error,
            new EventId(3000, "UnhandledException"),
            "Islenmemis hata: {Method} {Path} -> {StatusCode}");

    private static readonly Action<ILogger, string, string, int, string, Exception?> RequestRejectedMessage =
        LoggerMessage.Define<string, string, int, string>(
            LogLevel.Information,
            new EventId(3001, "RequestRejected"),
            "Istek reddedildi: {Method} {Path} -> {StatusCode} ({ExceptionType})");

    private static readonly Action<ILogger, Exception?> DatabaseUnreachableMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3002, "DatabaseUnreachable"),
            "Saglik kontrolu: veritabanina baglanilamadi.");

    private static readonly Action<ILogger, Exception?> DatabaseInitializingMessage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3003, "DatabaseInitializing"),
            "Development veritabani hazirlaniyor (migrate + seed)...");

    private static readonly Action<ILogger, Exception?> DatabaseReadyMessage =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3004, "DatabaseReady"),
            "Development veritabani hazir.");

    public static void UnhandledException(this ILogger logger, string method, string path, int statusCode, Exception exception) =>
        UnhandledExceptionMessage(logger, method, path, statusCode, exception);

    public static void RequestRejected(this ILogger logger, string method, string path, int statusCode, string exceptionType) =>
        RequestRejectedMessage(logger, method, path, statusCode, exceptionType, null);

    public static void DatabaseUnreachable(this ILogger logger, Exception exception) =>
        DatabaseUnreachableMessage(logger, exception);

    public static void DatabaseInitializing(this ILogger logger) => DatabaseInitializingMessage(logger, null);

    public static void DatabaseReady(this ILogger logger) => DatabaseReadyMessage(logger, null);
}
