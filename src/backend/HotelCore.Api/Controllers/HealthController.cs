using System.Data.Common;
using System.Diagnostics;
using HotelCore.Api.Startup;
using HotelCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Ayakta olma kontrolü (load balancer / CI smoke test). Anonimdir ve iç detay sızdırmaz:
/// veritabanı erişilemezse yalnızca "Unhealthy" bilgisi döner, hata mesajı log'da kalır.
/// </summary>
[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class HealthController(AppDbContext database, ILogger<HealthController> logger) : ControllerBase
{
    private const string Healthy = "Healthy";
    private const string Unhealthy = "Unhealthy";

    /// <summary>Uygulama ve veritabanı bağlantısının durumu.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var databaseHealthy = await CanConnectAsync(cancellationToken).ConfigureAwait(false);
        var elapsedMs = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

        var response = new HealthResponse(
            databaseHealthy ? Healthy : Unhealthy,
            databaseHealthy ? Healthy : Unhealthy,
            elapsedMs,
            DateTime.UtcNow);

        return databaseHealthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await database.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception)
        {
            logger.DatabaseUnreachable(exception);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            logger.DatabaseUnreachable(exception);
            return false;
        }
    }

    /// <summary>Sağlık kontrolü yanıtı.</summary>
    /// <param name="Status">Uygulamanın genel durumu.</param>
    /// <param name="Database">Veritabanı bağlantısının durumu.</param>
    /// <param name="DurationMs">Veritabanı kontrolünün süresi (ms).</param>
    /// <param name="TimestampUtc">Kontrol zamanı (UTC).</param>
    public sealed record HealthResponse(string Status, string Database, long DurationMs, DateTime TimestampUtc);
}
