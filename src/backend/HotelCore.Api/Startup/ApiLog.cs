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

    // --- Misafire açık kanal ---------------------------------------------------------------
    // DİKKAT: bu şablonların hiçbiri istek GÖVDESİ almaz. Public gövdeler hiçbir log seviyesinde
    // yazılmaz (architecture-public-booking.md §6.2); kart tuzak telinde yalnızca alan ADI ve yol
    // loglanır, değerin kendisi asla.

    private static readonly Action<ILogger, string, string, Exception?> CardDataRejectedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(3010, "CardDataRejected"),
            "Kart verisi tuzak teli tetiklendi: {Path} govdesinde '{FieldName}' alani var. " +
            "Istek reddedildi; govde LOGLANMADI.");

    private static readonly Action<ILogger, string, string, Exception?> RateLimitedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(3011, "PublicRateLimited"),
            "Public hiz siniri asildi: {Bucket} ({Path}).");

    private static readonly Action<ILogger, int, int, Exception?> HoldsSweptMessage =
        LoggerMessage.Define<int, int>(
            LogLevel.Debug,
            new EventId(3012, "PublicHoldsSwept"),
            "Suresi dolmus hold supurucusu: {ExpiredCount} suresi dolmus, {ConsumedCount} tuketilmis kayit silindi.");

    private static readonly Action<ILogger, Exception?> HoldSweepFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3013, "PublicHoldSweepFailed"),
            "Hold supurucusu bu turda basarisiz oldu; bir sonraki tur tekrar denenecek.");

    private static readonly Action<ILogger, string, string, Exception?> ConfirmationSentMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(3014, "BookingConfirmationSent"),
            "Rezervasyon onayi gonderildi (gelistirme uygulamasi): {BookingReference} -> {RecipientMasked}");

    private static readonly Action<ILogger, string, Exception?> ConfirmationFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3015, "BookingConfirmationFailed"),
            "Rezervasyon onayi gonderilemedi: {BookingReference}. Rezervasyon GECERLIDIR; " +
            "ConfirmationSentAt bos kalir ve eksiklik gorunur olur.");

    public static void CardDataRejected(this ILogger logger, string path, string fieldName) =>
        CardDataRejectedMessage(logger, path, fieldName, null);

    public static void PublicRateLimited(this ILogger logger, string bucket, string path) =>
        RateLimitedMessage(logger, bucket, path, null);

    public static void PublicHoldsSwept(this ILogger logger, int expiredCount, int consumedCount) =>
        HoldsSweptMessage(logger, expiredCount, consumedCount, null);

    public static void PublicHoldSweepFailed(this ILogger logger, Exception exception) =>
        HoldSweepFailedMessage(logger, exception);

    public static void BookingConfirmationSent(this ILogger logger, string reference, string recipientMasked) =>
        ConfirmationSentMessage(logger, reference, recipientMasked, null);

    public static void BookingConfirmationFailed(this ILogger logger, string reference, Exception exception) =>
        ConfirmationFailedMessage(logger, reference, exception);

    public static void UnhandledException(this ILogger logger, string method, string path, int statusCode, Exception exception) =>
        UnhandledExceptionMessage(logger, method, path, statusCode, exception);

    public static void RequestRejected(this ILogger logger, string method, string path, int statusCode, string exceptionType) =>
        RequestRejectedMessage(logger, method, path, statusCode, exceptionType, null);

    public static void DatabaseUnreachable(this ILogger logger, Exception exception) =>
        DatabaseUnreachableMessage(logger, exception);

    public static void DatabaseInitializing(this ILogger logger) => DatabaseInitializingMessage(logger, null);

    public static void DatabaseReady(this ILogger logger) => DatabaseReadyMessage(logger, null);
}
