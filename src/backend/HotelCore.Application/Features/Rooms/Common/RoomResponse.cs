namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// <c>RoomResponse</c> — api-contracts.md → "Şekiller" ile birebir.
/// <para>
/// <see cref="HousekeepingStatus"/> enum <b>adıdır</b> (string: <c>Clean | Dirty | Inspected |
/// OutOfOrder</c>), sayı değildir. <see cref="RoomTypeName"/> aktif dile göre çözümlenmiş
/// oda tipi adıdır.
/// </para>
/// </summary>
public sealed record RoomResponse
{
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public int Floor { get; init; }

    public Guid RoomTypeId { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    public string RoomTypeName { get; init; } = string.Empty;

    public string HousekeepingStatus { get; init; } = string.Empty;

    /// <summary>Servis dışı. <c>housekeepingStatus == OutOfOrder</c> ile tutarlı tutulur.</summary>
    public bool IsOutOfOrder { get; init; }

    public string? Note { get; init; }
}
