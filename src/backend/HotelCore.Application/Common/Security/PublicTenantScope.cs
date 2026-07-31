using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Application.Common.Security;

/// <summary>
/// Misafire açık kanalın <b>istek başına</b> tenant kapsamı. <c>PublicTenantMiddleware</c> her
/// public istekte önce <see cref="MarkPublicRequest"/> çağırır, sonra yoldaki <c>hotelSlug</c>'ı
/// çözebilirse <see cref="Activate"/> ile oteli kurar. <see cref="TenantContext"/> okur ve
/// <c>AppDbContext</c>'in global query filter'ı ona bakar.
///
/// <para><b><see cref="IsPublicRequest"/> neden ayrı bir bayrak</b> (oteli çözemediğimizde de
/// kurulur): public bir yolda <c>Authorization</c> header'ı gönderilebilir. Yalnızca
/// <see cref="HotelId"/>'ye baksaydık, slug çözülemediğinde tenant bağlamı sessizce <b>admin
/// kullanıcısının oteline</b> düşerdi — yani "admin token + geçersiz slug = başka otelin verisi"
/// yolu açılırdı. Bayrak, kimlik yolunu public istekte <b>tamamen</b> kapatır: otel çözülemezse
/// kapsam boş kalır ve hiçbir tenant satırı görünmez.</para>
///
/// <para><b><see cref="Enter"/> neden var — marka (brand) ucu:</b>
/// <c>GET /public/brands/{brandSlug}/hotels</c> birden çok otelin kapak görselini döndürür ve
/// <c>HotelImage</c> tenant-scoped'dır. Tek bir <c>HotelId</c> ile bu uç çalışamaz; alternatifler
/// <c>IgnoreQueryFilters()</c> ya da <c>CanAccessAllHotels = true</c> olurdu — <b>ikisi de public
/// yola bir filtre bypass'ı sokardı</b> (architecture-public-booking.md §4.2 bunu açıkça
/// yasaklar). Bunun yerine kapsam otelden otele <b>daraltılır</b>: her otelin görseli, yalnızca o
/// otelin kapsamı yürürlükteyken okunur. İzolasyon her sorguda tam korunur; bedeli otel sayısı
/// kadar (birkaç) küçük sorgudur. Aynı mekanizmayı arka plandaki hold süpürücüsü de kullanır.</para>
/// </summary>
public sealed class PublicTenantScope
{
    /// <summary>Aktif public otel; kapsam kurulmadıysa <c>null</c>.</summary>
    public Guid? HotelId { get; private set; }

    /// <summary>
    /// İstek public kanala mı ait. <c>true</c> olduğu sürece kimlik (JWT) tenant bağlamına
    /// <b>hiç</b> katılmaz.
    /// </summary>
    public bool IsPublicRequest { get; private set; }

    /// <summary>
    /// Otelin varsayılan dili — <c>Accept-Language</c> yoksa yanıt bu dilde üretilir
    /// (api-contracts-public-booking.md §1).
    /// </summary>
    public string? DefaultCulture { get; private set; }

    /// <summary>Yoldan okunan slug (log ve hız sınırı bölümleme anahtarı).</summary>
    public string? HotelSlug { get; private set; }

    /// <summary>İsteği public olarak işaretler; otel henüz çözülmemiş olabilir.</summary>
    public void MarkPublicRequest() => IsPublicRequest = true;

    /// <summary>
    /// Kapsamı kurar (middleware). Bir istek boyunca <b>bir kez</b> çağrılır; ikinci çağrı bir
    /// programlama hatasıdır (aynı istekte iki farklı otel kapsamı anlamsızdır).
    /// </summary>
    public void Activate(Guid hotelId, string hotelSlug, string defaultCulture)
    {
        if (HotelId is not null)
        {
            throw new InvalidOperationException(
                "Public tenant kapsami bu istekte zaten kuruldu; ikinci kez kurulamaz.");
        }

        IsPublicRequest = true;
        HotelId = hotelId;
        HotelSlug = hotelSlug;
        DefaultCulture = defaultCulture;
    }

    /// <summary>
    /// Kapsamı geçici olarak <paramref name="hotelId"/>'ye daraltır; <c>Dispose</c> önceki
    /// kapsamı <b>aynen</b> geri koyar.
    /// </summary>
    public IDisposable Enter(Guid hotelId) => new ScopeChange(this, hotelId);

    private sealed class ScopeChange : IDisposable
    {
        private readonly PublicTenantScope _owner;
        private readonly Guid? _previousHotelId;
        private readonly bool _previousIsPublic;

        public ScopeChange(PublicTenantScope owner, Guid hotelId)
        {
            _owner = owner;
            _previousHotelId = owner.HotelId;
            _previousIsPublic = owner.IsPublicRequest;

            owner.HotelId = hotelId;
            owner.IsPublicRequest = true;
        }

        public void Dispose()
        {
            _owner.HotelId = _previousHotelId;
            _owner.IsPublicRequest = _previousIsPublic;
        }
    }
}
