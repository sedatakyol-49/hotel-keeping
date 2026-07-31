namespace HotelCore.Application.Common.Exceptions;

/// <summary>
/// Misafire açık kanalın hata kataloğu (api-contracts-public-booking.md §8).
/// <para>
/// <b>Neden dilden bağımsız sabit anahtarlar:</b> istemci mantığı <c>status</c> + <c>code</c>
/// çiftine dayanır, <b>mesaj metnine asla</b>. Metin çevrilir ve yeniden yazılır; anahtar
/// sözleşmenin parçasıdır ve değişmez.
/// </para>
/// </summary>
public static class PublicErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";

    public const string CardDataNotAccepted = "CARD_DATA_NOT_ACCEPTED";

    public const string ChannelNotConfigured = "CHANNEL_NOT_CONFIGURED";

    public const string BrandNotFound = "BRAND_NOT_FOUND";

    public const string HotelNotFound = "HOTEL_NOT_FOUND";

    public const string RoomTypeNotFound = "ROOM_TYPE_NOT_FOUND";

    public const string HoldNotFound = "HOLD_NOT_FOUND";

    public const string BookingNotFound = "BOOKING_NOT_FOUND";

    public const string HoldExpired = "HOLD_EXPIRED";

    public const string HoldAlreadyUsed = "HOLD_ALREADY_USED";

    public const string RoomNoLongerAvailable = "ROOM_NO_LONGER_AVAILABLE";

    public const string CapacityExceeded = "CAPACITY_EXCEEDED";

    public const string SummaryChanged = "SUMMARY_CHANGED";

    public const string LegalTextChanged = "LEGAL_TEXT_CHANGED";

    public const string CancellationNotAllowed = "CANCELLATION_NOT_ALLOWED";

    public const string FeeAcknowledgementRequired = "FEE_ACKNOWLEDGEMENT_REQUIRED";

    public const string BookingAlreadyCancelled = "BOOKING_ALREADY_CANCELLED";

    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";

    public const string PaymentProviderUnavailable = "PAYMENT_PROVIDER_UNAVAILABLE";
}

/// <summary>
/// Public kanal hatası: durum kodunu <b>ve</b> stabil <c>code</c> anahtarını birlikte taşır.
/// <para>
/// <b>Neden mevcut istisna tiplerinin alt sınıfı değil:</b> <see cref="ConflictException"/> ve
/// kardeşleri admin sözleşmesinin parçasıdır ve <c>code</c> alanı taşımazlar. Onlara opsiyonel
/// bir alan eklemek, admin yanıtlarında bazen dolu bazen boş bir uzantı üretir — sözleşme
/// belirsizleşir. Ayrı bir tip, "bu hata public sözleşmenin kataloğundandır" bilgisini tipin
/// kendisinde tutar ve <c>ApiExceptionHandler</c> tek bir dalda ele alır.
/// </para>
/// <para>
/// <b>401/403 üretilmez:</b> public tarafta her yetki/varlık sorunu <b>404</b>'e indirgenir —
/// 403 dönmek sorulan kaynağın var olduğunu doğrulardı.
/// </para>
/// </summary>
public sealed class PublicApiException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    public PublicApiException()
        : this(500, PublicErrorCodes.ValidationFailed, "Public channel error.")
    {
    }

    public PublicApiException(string message)
        : this(500, PublicErrorCodes.ValidationFailed, message)
    {
    }

    public PublicApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = 500;
        Code = PublicErrorCodes.ValidationFailed;
        Errors = NoErrors;
    }

    public PublicApiException(
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors ?? NoErrors;
        RetryAfter = retryAfter;
    }

    /// <summary>HTTP durum kodu (404 / 409 / 400 / 429 / 503).</summary>
    public int StatusCode { get; }

    /// <summary>Dilden bağımsız hata anahtarı — <c>extensions.code</c>.</summary>
    public string Code { get; }

    /// <summary>Alan bazlı hatalar (yalnızca 400'lerde dolu).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>429 yanıtındaki <c>Retry-After</c> süresi.</summary>
    public TimeSpan? RetryAfter { get; }

    public static PublicApiException NotFound(string code, string message) =>
        new(404, code, message);

    public static PublicApiException Conflict(string code, string message) =>
        new(409, code, message);

    public static PublicApiException Conflict(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]> errors) =>
        new(409, code, message, errors);

    public static PublicApiException BadRequest(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(400, code, message, errors);

    /// <summary>
    /// Hız sınırı aşımı. <c>detail</c> <b>hangi eşiğin</b> aşıldığını söylemez: eşik bilgisi
    /// saldırganın sınırın hemen altında kalmasını kolaylaştırır.
    /// </summary>
    public static PublicApiException RateLimited(string message, TimeSpan retryAfter) =>
        new(429, PublicErrorCodes.RateLimitExceeded, message, errors: null, retryAfter: retryAfter);
}
