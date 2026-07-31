using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Application.Common.Security;

/// <summary>
/// <see cref="ITenantContext"/>'in tek implementasyonu: public kanal kapsamı ile JWT kimliğini
/// <b>birleştirmez</b>, aralarında seçim yapar.
///
/// <para><b>Public istek kimliği tamamen bastırır ve bu bilinçlidir:</b> public bir yolda
/// <c>Authorization</c> header'ı gönderilse bile kapsam yalnızca yoldaki slug'tan gelir. Slug
/// çözülemediyse kapsam <b>boş</b> kalır (hiçbir tenant satırı görünmez) — kimliğe düşülmez.
/// "Admin token + public uç = daha geniş veri" yolu böylece hiç açılmaz
/// (architecture-public-booking.md §4.2).</para>
///
/// <para><b>Kimlik yolunda davranış birebir korunur:</b> public kapsam yokken değerler doğrudan
/// <see cref="ICurrentUser"/>'dan okunur — <c>X-Hotel-Id</c> doğrulaması, varsayılan otel seçimi
/// ve Head Office konsolide modu değişmez.</para>
///
/// <para><b>Değişmez:</b> <c>Source == PublicChannel ⇒ HotelId != null &amp;&amp;
/// !CanAccessAllHotels</c>. İki koşul da tek satırda, tipin kendisinde zorlanır; yorumla değil
/// kodla korunur (ve bir sözleşme testiyle doğrulanır).</para>
/// </summary>
internal sealed class TenantContext(ICurrentUser currentUser, PublicTenantScope publicScope) : ITenantContext
{
    public TenantScopeSource Source
    {
        get
        {
            if (publicScope.IsPublicRequest)
            {
                // Otel çözülemediyse kaynak "None"dur: PublicChannel değeri HER ZAMAN bir otel
                // ifade etmelidir, aksi hâlde değişmez yalan söylerdi.
                return publicScope.HotelId is null
                    ? TenantScopeSource.None
                    : TenantScopeSource.PublicChannel;
            }

            return currentUser.IsAuthenticated ? TenantScopeSource.Authenticated : TenantScopeSource.None;
        }
    }

    public Guid? HotelId => publicScope.IsPublicRequest ? publicScope.HotelId : currentUser.HotelId;

    /// <summary>
    /// Public kanalda <b>her zaman</b> <c>false</c>: bypass yalnızca kimlik doğrulanmış Head
    /// Office kullanıcısına aittir.
    /// </summary>
    public bool CanAccessAllHotels => !publicScope.IsPublicRequest && currentUser.CanAccessAllHotels;
}
