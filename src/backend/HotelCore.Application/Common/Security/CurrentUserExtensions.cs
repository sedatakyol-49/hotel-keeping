using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Common.Security;

/// <summary>
/// <see cref="ICurrentUser"/> için kimlik bağlamı yardımcıları.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Yeni kayıt oluşturmak için aktif oteli döner. Head Office kullanıcısı <c>X-Hotel-Id</c>
    /// göndermediğinde bağlam <b>konsolide</b> olur (HotelId = null) ve kaydın hangi otele
    /// yazılacağı belirsizdir; bu durumda sessizce bir otel seçmek yerine 400 döndürülür.
    /// </summary>
    public static Guid RequireHotelId(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.HotelId
               ?? throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
               {
                   ["X-Hotel-Id"] = [Messages.HotelHeaderRequired]
               });
    }
}
