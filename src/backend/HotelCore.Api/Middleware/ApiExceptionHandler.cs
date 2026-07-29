using System.Diagnostics;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Exceptions;
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
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    private const string UnhandledTitle = "Beklenmeyen bir hata olustu.";

    private const string UnhandledDetail =
        "Istek islenirken beklenmeyen bir hata olustu. Destek talebinde traceId degerini paylasin.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

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

    private static (int StatusCode, string Title, string Detail) Map(Exception exception) => exception switch
    {
        ValidationException validation => (
            StatusCodes.Status400BadRequest,
            "Dogrulama hatasi.",
            validation.Message),
        AuthenticationException authentication => (
            StatusCodes.Status401Unauthorized,
            "Kimlik dogrulama basarisiz.",
            authentication.Message),
        ForbiddenException forbidden => (
            StatusCodes.Status403Forbidden,
            "Erisim reddedildi.",
            forbidden.Message),
        NotFoundException notFound => (
            StatusCodes.Status404NotFound,
            "Kayit bulunamadi.",
            notFound.Message),
        ConflictException conflict => (
            StatusCodes.Status409Conflict,
            "Islem mevcut durumla celisiyor.",
            conflict.Message),
        OperationCanceledException => (
            StatusCodes.Status499ClientClosedRequest,
            "Istek iptal edildi.",
            "Istemci istegi tamamlanmadan kapatti."),
        _ => (StatusCodes.Status500InternalServerError, UnhandledTitle, UnhandledDetail)
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
        StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        _ => "about:blank"
    };
}
