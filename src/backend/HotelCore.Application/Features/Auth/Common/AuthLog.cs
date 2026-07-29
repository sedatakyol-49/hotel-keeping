using Microsoft.Extensions.Logging;

namespace HotelCore.Application.Features.Auth.Common;

/// <summary>Auth slice'ının güvenlik log mesajları (LoggerMessage delegeleri — CA1848).</summary>
internal static class AuthLog
{
    private static readonly Action<ILogger, Guid, Exception?> RefreshTokenReuseDetectedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2000, "RefreshTokenReuseDetected"),
            "Iptal edilmis refresh token yeniden kullanildi; kullanicinin tum oturumlari kapatildi. UserId: {UserId}");

    public static void RefreshTokenReuseDetected(this ILogger logger, Guid userId) =>
        RefreshTokenReuseDetectedMessage(logger, userId, null);
}
