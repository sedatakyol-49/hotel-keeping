using Microsoft.Extensions.Logging;

namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Rapor modülünün log mesajları. <see cref="LoggerMessage"/> delegeleri kullanılır (CA1848:
/// şablon bir kez derlenir, seviye kapalıysa argümanlar kutulanmaz).
/// </summary>
internal static class ReportLog
{
    private static readonly Action<ILogger, decimal, decimal, decimal, decimal, Exception?> RevParMismatchMessage =
        LoggerMessage.Define<decimal, decimal, decimal, decimal>(
            LogLevel.Warning,
            new EventId(2100, "ReportRevParMismatch"),
            "RevPAR tutarsizligi: dogrudan={DirectRevPar}, ADRxDoluluk={DerivedRevPar} " +
            "(ADR={Adr}, doluluk={OccupancyRate}%). Metrik tanimlari ayrismis olabilir.");

    /// <summary>
    /// <c>RevPAR = ADR × doluluk</c> özdeşliğinin iki yoldan doğrulanması. Tanım gereği
    /// tutmalıdır (üç metrik de aynı <c>satılan</c>/<c>müsait</c> sayılarını kullanır); sapma
    /// yalnızca birileri tanımlardan birini değiştirirse oluşur — bu yüzden sessiz kalmak yerine
    /// uyarı loglanır (regresyon ağı).
    /// </summary>
    public static void RevParMismatch(
        this ILogger logger,
        decimal directRevPar,
        decimal derivedRevPar,
        decimal adr,
        decimal occupancyRate) =>
        RevParMismatchMessage(logger, directRevPar, derivedRevPar, adr, occupancyRate, null);
}
