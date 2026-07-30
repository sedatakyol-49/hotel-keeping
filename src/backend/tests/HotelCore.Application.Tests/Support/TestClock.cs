using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// Sabit (dondurulmus) saat. <c>DeleteRoomHandler</c> "gelecek tarihli rezervasyon" kuralini
/// bugunun tarihine gore degerlendirdigi icin testler gercek zamana bagli olmamalidir:
/// aksi halde gece yarisi veya yil sonu gibi anlarda kirilgan olurlardi.
/// </summary>
internal sealed class TestClock : IDateTimeProvider
{
    /// <summary>Testlerin referans ani (UTC). Ayin/yilin ortasi bilincli olarak secildi.</summary>
    public static readonly DateTimeOffset DefaultNow = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; set; } = DefaultNow;

    /// <summary>Saatin gosterdigi takvim gunu (handler'in <c>today</c> hesabiyla ayni).</summary>
    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
