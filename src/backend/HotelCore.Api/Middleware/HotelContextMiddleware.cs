using HotelCore.Api.Services;
using HotelCore.Application.Common.Interfaces;
using Serilog.Context;

namespace HotelCore.Api.Middleware;

/// <summary>
/// <c>X-Hotel-Id</c> header'ını istek boru hattının başında çözüp doğrular.
/// <para>
/// Doğrulama zaten <see cref="CurrentUser"/> içinde yapılır; bu middleware sonucu
/// <b>erkenden</b> tetikler. Aksi hâlde yetkisiz otel hatası, EF global query filter
/// değerlendirilirken (handler'ın ortasında) ortaya çıkardı. Böylece 403 yanıtı
/// endpoint hiç çalışmadan üretilir ve aktif otel tüm log satırlarına eklenir.
/// </para>
/// </summary>
public sealed class HotelContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentUser);

        // Public yolda X-Hotel-Id YOK SAYILIR (400 üretmez): otorite yoldaki hotelSlug'dadır ve
        // tenant kapsamı PublicTenantMiddleware tarafından zaten kurulmuştur. Burada
        // doğrulamayı tetiklemek, admin token'ı taşıyan bir public isteği 403 ile reddederdi —
        // oysa public uçlar kimliği tamamen görmezden gelmelidir.
        if (!currentUser.IsAuthenticated || PublicTenantMiddleware.IsPublicRequest(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Getter erişimi doğrulamayı çalıştırır: yetkisiz otel -> ForbiddenException (403).
        var hotelId = currentUser.HotelId;

        using (LogContext.PushProperty("UserId", currentUser.UserId))
        using (LogContext.PushProperty("HotelId", hotelId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
