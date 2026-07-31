namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Ödeme sağlayıcısı (PSP) soyutlaması — architecture-public-booking.md §6.1.
/// <para>
/// <b>Kart verisi bu arayüzden GEÇMEZ.</b> Kart yalnızca PSP'nin kendi iframe/SDK'sıyla alınır;
/// bize dönen tek şey opak bir <c>providerReference</c>'tır. Bir kez bile PAN kabul etmek tüm
/// API'yi, log altyapısını ve yedekleri PCI-DSS kapsamına sokar — geri dönüşü çok pahalı bir
/// eşiktir.
/// </para>
/// <para>
/// Bu fazda kayıtlı implementasyon <c>NullPaymentAuthorizationProvider</c>'dır
/// (<see cref="SupportsGuarantee"/> = <c>false</c>): <c>paymentOptions</c> yalnızca
/// <c>PayAtProperty</c> döner ve garanti istenirse <c>400 CHANNEL_NOT_CONFIGURED</c> üretilir —
/// istek sessizce yok sayılmaz, sözleşme yalan söylemez.
/// </para>
/// </summary>
public interface IPaymentAuthorizationProvider
{
    /// <summary>Sağlayıcı anahtarı (<c>none</c> | <c>stripe</c> | <c>adyen</c> …).</summary>
    string Key { get; }

    /// <summary>Kart garantisi destekleniyor mu.</summary>
    bool SupportsGuarantee { get; }

    /// <summary>Garanti yetkilendirmesi (bu fazda çağrılmaz).</summary>
    Task<GuaranteeAuthorization> AuthorizeAsync(GuaranteeRequest request, CancellationToken cancellationToken);

    /// <summary>Yetkilendirmeyi geri alır.</summary>
    Task VoidAsync(string providerReference, CancellationToken cancellationToken);
}

/// <summary>Garanti isteği — <b>kart alanı yoktur ve eklenmeyecektir</b>.</summary>
/// <param name="BookingReference">Misafire gösterilen referans.</param>
/// <param name="Amount">Garanti tutarı.</param>
/// <param name="Currency">ISO 4217 kodu.</param>
/// <param name="ChallengeToken">PSP'nin istemci tarafında ürettiği opak belirteç.</param>
public sealed record GuaranteeRequest(
    string BookingReference,
    decimal Amount,
    string Currency,
    string? ChallengeToken);

/// <summary>Garanti sonucu — yalnızca opak bir sağlayıcı referansı taşır.</summary>
/// <param name="ProviderReference">PSP'nin token'ı; tek başına para çekmez.</param>
/// <param name="AuthorizedAmount">Yetkilendirilen tutar.</param>
public sealed record GuaranteeAuthorization(string ProviderReference, decimal AuthorizedAmount);
