namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Aktif tenant kapsamının <b>nereden</b> geldiği. Kaynak bilinmeden kapsam denetlenemez:
/// aynı <c>HotelId = null</c> değeri "kimliksiz istek" (hiçbir şey görünmez) ve "Head Office
/// konsolide görünüm" (her şey görünür) hâllerinde <b>zıt</b> anlamlara gelir.
/// </summary>
public enum TenantScopeSource
{
    /// <summary>Kimlik yok ve public kanal kapsamı da kurulmadı — hiçbir tenant satırı görünmez.</summary>
    None = 0,

    /// <summary>JWT ile gelen istek; kapsam <see cref="ICurrentUser"/> davranışının birebir aynısıdır.</summary>
    Authenticated = 1,

    /// <summary>
    /// Misafire açık (anonim) kanal; otel <b>yol parametresindeki slug</b>'dan çözülmüştür
    /// (architecture-public-booking.md §4.2).
    /// </summary>
    PublicChannel = 2
}

/// <summary>
/// <c>AppDbContext</c>'in global query filter'ının okuduğu <b>tek</b> tenant kaynağı.
/// <para>
/// <b>Neden <see cref="ICurrentUser"/> yetmiyor:</b> public kanal anonimdir — JWT yoktur, ama
/// filtreye <b>kesin</b> bir otel verilmelidir. <see cref="ICurrentUser"/> kimliksiz istekte
/// <c>null</c> döndürmek zorundadır (güvenli varsayılan), dolayısıyla anonim ama kapsamlı bir
/// istek o arayüzle ifade edilemez. Kapsam kavramı kimlikten ayrılır: kimlik "kim" sorusunu,
/// tenant bağlamı "hangi otelin verisi" sorusunu yanıtlar.
/// </para>
/// <para>
/// <b>Değişmez (invariant), testle korunur:</b>
/// <c>Source == PublicChannel ⇒ HotelId != null &amp;&amp; CanAccessAllHotels == false</c>.
/// Bu iki koşuldan biri bozulursa public bir istek ya hiçbir şey görür ya <b>her şeyi</b> görür.
/// </para>
/// </summary>
public interface ITenantContext
{
    /// <summary>Aktif otel; <c>null</c> ise (kaynağa göre) ya hiçbir şey ya her şey görünür.</summary>
    Guid? HotelId { get; }

    /// <summary>
    /// Head Office konsolide görünümü — filtrenin <b>tek</b> bypass noktası.
    /// Public kanalda <b>her zaman</b> <c>false</c>'tur.
    /// </summary>
    bool CanAccessAllHotels { get; }

    /// <summary>Kapsamın kaynağı.</summary>
    TenantScopeSource Source { get; }
}
