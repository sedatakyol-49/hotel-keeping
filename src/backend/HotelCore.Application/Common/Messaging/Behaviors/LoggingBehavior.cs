using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HotelCore.Application.Common.Messaging.Behaviors;

/// <summary>
/// Use-case başına yapılandırılmış log (Serilog): istek adı + süre. Yavaş handler'lar
/// uyarı seviyesine yükseltilir. İstek gövdesi LOGLANMAZ (parola/PII sızıntısı riski).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Bu eşiğin üzerindeki handler'lar uyarı olarak raporlanır.</summary>
    private const long SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var requestName = typeof(TRequest).Name;
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            var response = await continuation().ConfigureAwait(false);

            var elapsedMs = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            if (elapsedMs >= SlowRequestThresholdMs)
            {
                logger.UseCaseSlow(requestName, elapsedMs);
            }
            else
            {
                logger.UseCaseCompleted(requestName, elapsedMs);
            }

            return response;
        }
        catch (Exception exception)
        {
            logger.UseCaseFailed(
                requestName,
                (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds,
                exception);
            throw;
        }
    }
}
