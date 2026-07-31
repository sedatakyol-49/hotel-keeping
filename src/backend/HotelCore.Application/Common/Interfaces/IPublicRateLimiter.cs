namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Public kanalın hız sınırı deposu (api-contracts-public-booking.md §1.2).
/// <para>
/// <b>Neden Application katmanında bir port:</b> IP bazlı sınır bir HTTP kaygısıdır ve
/// middleware'de uygulanır, ama <b>e-posta bazlı</b> sınır ancak gövde çözümlendikten sonra —
/// yani handler'ın içinde — uygulanabilir (<c>POST /bookings</c> 3/saat, <c>lookup</c> 3/saat).
/// İki farklı sayaç deposu tutmak, aynı kuralın iki yerde ayrışmasına yol açardı.
/// </para>
/// <para>
/// <b>Ham e-posta saklanmaz:</b> anahtar <c>SHA-256(lower(email))</c>'dir. Sınırlar
/// yapılandırmadan gelir, koda gömülmez.
/// </para>
/// </summary>
public interface IPublicRateLimiter
{
    /// <summary>
    /// Bir jeton almayı dener.
    /// </summary>
    /// <param name="bucket">Kural adı (<c>public.holds</c>, <c>public.bookings.email</c> …).</param>
    /// <param name="partitionKey">Bölümleme anahtarı (otel + IP ya da otel + e-posta özeti).</param>
    /// <param name="retryAfter">Reddedildiyse istemcinin beklemesi gereken süre.</param>
    /// <returns>İstek kabul edildiyse <c>true</c>.</returns>
    bool TryAcquire(string bucket, string partitionKey, out TimeSpan retryAfter);
}

/// <summary>Hız sınırı kural adları — middleware ve handler'lar aynı anahtarları kullanır.</summary>
public static class PublicRateLimitBuckets
{
    /// <summary>Katalog / künye / hukuki bilgi uçları (varsayılan 120/dk).</summary>
    public const string Catalog = "public.catalog";

    /// <summary>Müsaitlik araması (varsayılan 60/dk).</summary>
    public const string Availability = "public.availability";

    /// <summary>Hold oluşturma (varsayılan 10/dk).</summary>
    public const string HoldCreate = "public.holds.create";

    /// <summary>Hold okuma (varsayılan 60/dk).</summary>
    public const string HoldRead = "public.holds.read";

    /// <summary>Hold bırakma (varsayılan 30/dk).</summary>
    public const string HoldRelease = "public.holds.release";

    /// <summary>Rezervasyon oluşturma — IP (varsayılan 5/saat).</summary>
    public const string BookingCreate = "public.bookings.create";

    /// <summary>Rezervasyon oluşturma — e-posta özeti (varsayılan 3/saat).</summary>
    public const string BookingCreateEmail = "public.bookings.create.email";

    /// <summary>Rezervasyon okuma (varsayılan 30/dk).</summary>
    public const string BookingRead = "public.bookings.read";

    /// <summary>Online iptal (varsayılan 10/saat).</summary>
    public const string BookingCancel = "public.bookings.cancel";

    /// <summary>Bağlantı yeniden gönderimi — IP (varsayılan 5/saat).</summary>
    public const string BookingLookup = "public.bookings.lookup";

    /// <summary>Bağlantı yeniden gönderimi — e-posta özeti (varsayılan 3/saat).</summary>
    public const string BookingLookupEmail = "public.bookings.lookup.email";
}
