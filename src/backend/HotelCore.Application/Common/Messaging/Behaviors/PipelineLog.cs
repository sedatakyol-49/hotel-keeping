using Microsoft.Extensions.Logging;

namespace HotelCore.Application.Common.Messaging.Behaviors;

/// <summary>
/// Boru hattı log mesajları. <see cref="LoggerMessage"/> delegeleri kullanılır: mesaj şablonu
/// bir kez derlenir, log seviyesi kapalıysa argümanlar kutulanmaz (CA1848).
/// </summary>
internal static class PipelineLog
{
    private static readonly Action<ILogger, string, long, Exception?> UseCaseCompletedMessage =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(1000, "UseCaseCompleted"),
            "Use-case {RequestName} tamamlandi ({ElapsedMilliseconds} ms)");

    private static readonly Action<ILogger, string, long, Exception?> UseCaseSlowMessage =
        LoggerMessage.Define<string, long>(
            LogLevel.Warning,
            new EventId(1001, "UseCaseSlow"),
            "Use-case {RequestName} yavas tamamlandi ({ElapsedMilliseconds} ms)");

    private static readonly Action<ILogger, string, long, Exception?> UseCaseFailedMessage =
        LoggerMessage.Define<string, long>(
            LogLevel.Warning,
            new EventId(1002, "UseCaseFailed"),
            "Use-case {RequestName} hata ile sonlandi ({ElapsedMilliseconds} ms)");

    public static void UseCaseCompleted(this ILogger logger, string requestName, long elapsedMilliseconds) =>
        UseCaseCompletedMessage(logger, requestName, elapsedMilliseconds, null);

    public static void UseCaseSlow(this ILogger logger, string requestName, long elapsedMilliseconds) =>
        UseCaseSlowMessage(logger, requestName, elapsedMilliseconds, null);

    public static void UseCaseFailed(this ILogger logger, string requestName, long elapsedMilliseconds, Exception exception) =>
        UseCaseFailedMessage(logger, requestName, elapsedMilliseconds, exception);
}
