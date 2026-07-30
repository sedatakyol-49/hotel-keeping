using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// <c>isOutOfOrder</c> ↔ <c>housekeepingStatus = OutOfOrder</c> tutarlılığının tek uygulandığı yer
/// (api-contracts.md → "Doğrulama kuralları"):
/// <list type="bullet">
///   <item>durum <c>OutOfOrder</c>'a çekilirse <c>isOutOfOrder</c> <b>true</b> olur,</item>
///   <item><c>OutOfOrder</c>'dan çıkılırsa <b>false</b> olur,</item>
///   <item>istekte yalnızca <c>isOutOfOrder = true</c> gönderilmişse durum da <c>OutOfOrder</c>
///         yapılır (aynı değişmezin diğer yönü).</item>
/// </list>
/// Böylece veritabanında "servis dışı ama durumu Clean" gibi çelişkili satır oluşamaz.
/// </summary>
internal static class HousekeepingState
{
    /// <summary>İstenen durum/bayrak çiftini tutarlı hâle getirir.</summary>
    public static (HousekeepingStatus Status, bool IsOutOfOrder) Reconcile(
        HousekeepingStatus status,
        bool isOutOfOrder) =>
        status is HousekeepingStatus.OutOfOrder || isOutOfOrder
            ? (HousekeepingStatus.OutOfOrder, true)
            : (status, false);

    /// <summary>Tutarlılık kuralını odaya uygular.</summary>
    public static void Apply(Room room, HousekeepingStatus status, bool isOutOfOrder)
    {
        ArgumentNullException.ThrowIfNull(room);

        (room.HousekeepingStatus, room.IsOutOfOrder) = Reconcile(status, isOutOfOrder);
    }
}
