namespace HotelCore.Application.Features.RatePlans.Common;

/// <summary>
/// Fiyat planı — api-contracts-reservations.md → "Rate Plans" ile birebir.
/// </summary>
public sealed record RatePlanResponse
{
    public Guid Id { get; init; }

    public Guid RoomTypeId { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    /// <summary>Oda tipinin varsayılan dildeki adı (çeviri çözümlemesi yapılmaz).</summary>
    public string RoomTypeName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Gecelik fiyat (brüt, otelin para biriminde).</summary>
    public decimal Price { get; init; }

    public string Currency { get; init; } = string.Empty;

    /// <summary>Geçerlilik başlangıcı (dahil).</summary>
    public DateOnly ValidFrom { get; init; }

    /// <summary>Geçerlilik bitişi (<b>dahil</b> — kapalı aralık).</summary>
    public DateOnly ValidTo { get; init; }

    /// <summary>
    /// Kanal — <c>ReservationChannel</c> enum <b>adı</b> (string). <c>null</c> ise plan
    /// <b>tüm kanallar</b> için geçerlidir.
    /// </summary>
    public string? Channel { get; init; }

    /// <summary>Pasif planlar fiyat hesabına girmez ve çakışma kontrolünde sayılmaz.</summary>
    public bool IsActive { get; init; }
}
