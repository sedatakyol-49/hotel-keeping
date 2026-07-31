using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Api.Services;

/// <summary>
/// Varsayılan ödeme sağlayıcısı: <b>hiçbiri</b>. Bu fazda ödeme <b>girişte</b> yapılır, kart
/// garantisi alınmaz (architecture-public-booking.md §6.1).
///
/// <para><b>Neden bir "null" implementasyon var, neden servis hiç kaydedilmiyor değil:</b>
/// kayıtsız bırakmak, garanti isteyen bir isteğin <c>NullReferenceException</c> ile 500 üretmesi
/// demek olurdu. Açık bir "desteklemiyorum" cevabı, sözleşmedeki
/// <c>400 CHANNEL_NOT_CONFIGURED</c> davranışını mümkün kılar: istek <b>sessizce yok
/// sayılmaz</b>.</para>
///
/// <para><b>Kart verisi bu sınıftan da geçmez</b> ve geçmeyecektir; PSP takıldığında değişecek
/// tek yer bu tipin yerine gelecek implementasyondur — DTO'lar ve uç yolları değişmez.</para>
/// </summary>
public sealed class NullPaymentAuthorizationProvider : IPaymentAuthorizationProvider
{
    public string Key => "none";

    public bool SupportsGuarantee => false;

    public Task<GuaranteeAuthorization> AuthorizeAsync(
        GuaranteeRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "Kart garantisi bu kurulumda yapilandirilmamistir (IPaymentAuthorizationProvider = none). " +
            "Uygulama katmani bu duruma 400 CHANNEL_NOT_CONFIGURED ile yanit verir; bu istisnaya " +
            "ulasilmasi bir programlama hatasidir.");

    public Task VoidAsync(string providerReference, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
