using System.Security.Claims;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Security;

namespace HotelCore.Api.Services;

/// <summary>
/// <see cref="ICurrentUser"/>'ın HTTP implementasyonu: değerler JWT claim'lerinden
/// (bkz. api-contracts.md "JWT Claim Şeması") ve <c>X-Hotel-Id</c> header'ından okunur.
/// <para>
/// <b>Aktif otel çözümü:</b>
/// <list type="number">
///   <item><c>X-Hotel-Id</c> gönderilmişse: değer GUID olmalıdır (aksi hâlde 400). Kullanıcının
///         o otele erişimi <c>hotel</c> claim'leri veya <c>allHotels</c> ile <b>doğrulanır</b>;
///         yetkisizse <see cref="ForbiddenException"/> → 403.</item>
///   <item>Header yoksa ve kullanıcı Head Office ise: <c>null</c> → konsolide görünüm.</item>
///   <item>Header yoksa: JWT'deki ilk <c>hotel</c> claim'i (varsayılan otel) kullanılır.</item>
/// </list>
/// Sonuç istek başına bir kez hesaplanıp önbelleklenir; global query filter her sorguda okur.
/// </para>
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    /// <summary>Aktif oteli değiştiren istek header'ı (api-contracts.md "Genel Kurallar").</summary>
    public const string HotelHeaderName = "X-Hotel-Id";

    private bool _hotelResolved;
    private Guid? _resolvedHotelId;

    public Guid? UserId => ParseGuid(FindClaim(JwtClaimNames.Subject));

    public Guid? HeadOfficeId => ParseGuid(FindClaim(JwtClaimNames.HeadOfficeId));

    public bool CanAccessAllHotels =>
        bool.TryParse(FindClaim(JwtClaimNames.AllHotels), out var value) && value;

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(JwtClaimNames.Permission).Select(c => c.Value).ToArray() ?? [];

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? HotelId
    {
        get
        {
            if (_hotelResolved)
            {
                return _resolvedHotelId;
            }

            _resolvedHotelId = ResolveHotelId();
            _hotelResolved = true;

            return _resolvedHotelId;
        }
    }

    /// <summary>Kullanıcının JWT'de taşınan otel erişim listesi (sıra korunur).</summary>
    private IReadOnlyList<Guid> AccessibleHotelIds =>
        Principal?.FindAll(JwtClaimNames.Hotel)
            .Select(c => ParseGuid(c.Value))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray() ?? [];

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    private Guid? ResolveHotelId()
    {
        if (!IsAuthenticated)
        {
            // Kimliksiz istek hiçbir tenant satırını görmemelidir.
            return null;
        }

        var accessibleHotelIds = AccessibleHotelIds;
        var requestedHotelId = ReadRequestedHotelId();

        if (requestedHotelId is Guid hotelId)
        {
            if (CanAccessAllHotels || accessibleHotelIds.Contains(hotelId))
            {
                return hotelId;
            }

            throw new ForbiddenException(Messages.HotelAccessDenied(hotelId));
        }

        // Head Office kullanıcısı header göndermediyse konsolide (tüm oteller) görünüm ister.
        if (CanAccessAllHotels)
        {
            return null;
        }

        // Sıra anlamlıdır: ilk "hotel" claim'i kullanıcının varsayılan otelidir.
        return accessibleHotelIds.Count > 0 ? accessibleHotelIds[0] : null;
    }

    private Guid? ReadRequestedHotelId()
    {
        var headerValue = httpContextAccessor.HttpContext?.Request.Headers[HotelHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        if (!Guid.TryParse(headerValue, out var hotelId))
        {
            throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [HotelHeaderName] = [Messages.InvalidGuid]
            });
        }

        return hotelId;
    }

    private string? FindClaim(string claimType) => Principal?.FindFirst(claimType)?.Value;

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
