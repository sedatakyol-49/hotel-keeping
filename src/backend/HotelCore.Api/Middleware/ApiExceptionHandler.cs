using System.Diagnostics;
using System.Globalization;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ValidationException = HotelCore.Application.Common.Exceptions.ValidationException;

namespace HotelCore.Api.Middleware;

/// <summary>
/// Merkezî hata dönüştürücü: Application katmanı istisnalarını RFC 7807
/// <see cref="ProblemDetails"/> yanıtlarına mapler.
/// <list type="bullet">
///   <item><see cref="ValidationException"/> → 400 (+ <c>errors</c> sözlüğü)</item>
///   <item><see cref="AuthenticationException"/> → 401</item>
///   <item><see cref="ForbiddenException"/> → 403</item>
///   <item><see cref="NotFoundException"/> → 404</item>
///   <item><see cref="ConflictException"/> → 409</item>
///   <item>diğer → 500 (genel mesaj)</item>
/// </list>
/// <para>
/// <b>Bilgi sızıntısı:</b> beklenmeyen hatalarda istemciye yalnızca genel bir mesaj ve
/// <c>traceId</c> döner; istisna mesajı/stack trace <b>sadece Development'ta</b> eklenir.
/// Ayrıntı her hâlükârda sunucu log'una yazılır.
/// </para>
/// <para>
/// <b>i18n:</b> <c>title</c>/<c>detail</c> metinleri <see cref="Messages"/> üzerinden isteğin
/// dilinde üretilir; <c>detail</c> zaten Application katmanında yerelleştirilmiş istisna
/// mesajıdır. Böylece <c>title</c>, <c>detail</c> ve <c>errors</c> aynı dilde döner. Yanıt bu
/// dili <c>Content-Language</c> başlığıyla da <b>bildirir</b>
/// (<see cref="ProblemDetailsResponseHeaders"/>). Log mesajları bilinçli olarak çevrilmez
/// (geliştiriciye yöneliktir).
/// </para>
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Bu handler boru hattında UseRequestLocalization'dan DAHA DIŞARIDA çalışır; o yüzden
        // isteğin dili yeniden yürürlüğe konmadan başlıklar sunucu dilinde üretilirdi
        // (bkz. RequestCultureScope).
        using var culture = RequestCultureScope.Apply(httpContext);

        // ExceptionHandlerMiddleware yanıtı sıfırladığı için localization middleware'in yazdığı
        // Content-Language silinmiştir; sözleşme gereği (api-contracts.md) hata yanıtı da aktif
        // dili bildirmelidir. charset de burada garanti altına alınır — bkz. sınıf belgesi.
        ProblemDetailsResponseHeaders.Apply(httpContext);

        var (statusCode, title, detail) = Map(exception);

        LogException(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = TypeUriFor(statusCode),
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        // Korelasyon: log'daki kayıtla eşleştirmek için her yanıtta bulunur.
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        ApplyPublicChannelExtensions(httpContext, exception, problemDetails, statusCode);

        if (statusCode >= StatusCodes.Status500InternalServerError && environment.IsDevelopment())
        {
            // Yalnızca Development: teşhis kolaylığı. Production'da ASLA eklenmez.
            problemDetails.Extensions["exception"] = exception.ToString();
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Public kanala özgü <c>ProblemDetails</c> uzantıları (api-contracts-public-booking.md §1).
    ///
    /// <para><b><c>code</c> neden var:</b> istemci mantığı <c>status</c> + <c>code</c> çiftine
    /// dayanır, <b>mesaj metnine asla</b>. Metin çevrilir ve yeniden yazılır; anahtar sözleşmenin
    /// parçasıdır. Admin uçları bu alanı bu fazda taşımaz ve yokluğu bir hata değildir.</para>
    ///
    /// <para><b>Neden yola bakılıyor:</b> FluentValidation'dan gelen genel bir
    /// <c>ValidationException</c> public bir uçta da doğabilir; o durumda kod açıkça
    /// <c>VALIDATION_FAILED</c>'dır. Public olmayan yollarda hiçbir uzantı eklenmez, yani admin
    /// sözleşmesi değişmez.</para>
    /// </summary>
    private static void ApplyPublicChannelExtensions(
        HttpContext httpContext,
        Exception exception,
        ProblemDetails problemDetails,
        int statusCode)
    {
        if (exception is PublicApiException publicException)
        {
            problemDetails.Extensions["code"] = publicException.Code;

            if (publicException.Errors.Count > 0)
            {
                problemDetails.Extensions["errors"] = publicException.Errors;
            }

            if (publicException.RetryAfter is TimeSpan retryAfter && !httpContext.Response.HasStarted)
            {
                // 429 sözleşmesi Retry-After'ı ZORUNLU kılar; saniye cinsinden ve en az 1.
                httpContext.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        .ToString(CultureInfo.InvariantCulture);
            }

            return;
        }

        if (!PublicTenantMiddleware.IsPublicRequest(httpContext.Request.Path))
        {
            return;
        }

        // Public yolda doğan ama katalogda karşılığı olmayan hatalar: yalnızca doğrulama ve hız
        // sınırı sınıfları anlamlı bir stabil anahtara sahiptir.
        var derived = statusCode switch
        {
            StatusCodes.Status400BadRequest => PublicErrorCodes.ValidationFailed,
            StatusCodes.Status429TooManyRequests => PublicErrorCodes.RateLimitExceeded,
            _ => null
        };

        if (derived is not null)
        {
            problemDetails.Extensions["code"] = derived;
        }
    }

    private static (int StatusCode, string Title, string Detail) Map(Exception exception) => exception switch
    {
        // Public kanal hataları kendi durum kodlarını taşır; başlık yine yerelleştirilir.
        PublicApiException publicException => (
            publicException.StatusCode,
            Messages.TitleForStatusCode(publicException.StatusCode) ?? Messages.BadRequestTitle,
            publicException.Message),
        ValidationException validation => (
            StatusCodes.Status400BadRequest,
            Messages.BadRequestTitle,
            validation.Message),
        AuthenticationException authentication => (
            StatusCodes.Status401Unauthorized,
            Messages.UnauthorizedTitle,
            authentication.Message),
        ForbiddenException forbidden => (
            StatusCodes.Status403Forbidden,
            Messages.ForbiddenTitle,
            forbidden.Message),
        NotFoundException notFound => (
            StatusCodes.Status404NotFound,
            Messages.NotFoundTitle,
            notFound.Message),
        ConflictException conflict => (
            StatusCodes.Status409Conflict,
            Messages.ConflictTitle,
            conflict.Message),
        OperationCanceledException => (
            StatusCodes.Status499ClientClosedRequest,
            Messages.ClientClosedRequestTitle,
            Messages.ClientClosedRequestDetail),
        _ => (
            StatusCodes.Status500InternalServerError,
            Messages.UnhandledTitle,
            Messages.UnhandledDetail)
    };

    private void LogException(HttpContext httpContext, Exception exception, int statusCode)
    {
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.ToString();

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.UnhandledException(method, path, statusCode, exception);
            return;
        }

        logger.RequestRejected(method, path, statusCode, exception.GetType().Name);
    }

    /// <summary>RFC 9110'daki durum kodu tanımlarına işaret eden <c>type</c> URI'ları.</summary>
    private static string TypeUriFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status429TooManyRequests => "https://tools.ietf.org/html/rfc6585#section-4",
        StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        _ => "about:blank"
    };
}
