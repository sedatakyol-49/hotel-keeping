using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.CreateBooking;

/// <summary>§312j kanıt kaydı — istemcinin gösterdiği düğme metni ve onayladığı özet.</summary>
public sealed record PublicCheckoutRequest
{
    /// <summary><c>sha256:</c> + 64 hex; hold'daki değerle karşılaştırılır.</summary>
    public string SummaryHash { get; init; } = string.Empty;

    /// <summary>
    /// Ekranda gösterilen sipariş düğmesi metni. <b>Sunucu bunu DOĞRULAMAZ, KAYDEDER</b>:
    /// dil/varyant meşru olabilir ve sunucunun istemci ekranını görmesi mümkün değildir.
    /// Yapılabilecek tek şey uyuşmazlık hâlinde kanıt saklamaktır.
    /// </summary>
    public string OrderButtonLabel { get; init; } = string.Empty;
}

/// <summary>
/// Misafir künyesi. <b>Doğum tarihi, uyrukluk, kimlik/pasaport no ve tam ev adresi
/// SORULMAZ</b> — bunlar Meldeschein verisidir (BMG §§29–30) ve <b>girişte</b> alınır.
/// </summary>
public sealed record PublicGuestRequest
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    /// <summary>§312f onayının kalıcı veri taşıyıcısı; iptal bağlantısı buraya gider.</summary>
    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    /// <summary>Yazışma ve fatura dili (<c>de</c> | <c>en</c> | <c>tr</c>).</summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary>
    /// Beyan edilen <b>ikamet</b> ülkesi (opsiyonel). <c>Guest.Nationality</c>'den farklıdır:
    /// uyrukluk Meldeschein verisidir ve sorulmaz.
    /// </summary>
    public string? CountryOfResidence { get; init; }
}

/// <summary>Opsiyonel kurumsal fatura künyesi (§33 UStDV: küçük tutarlı faturada aranmaz).</summary>
public sealed record PublicInvoiceAddressRequest
{
    public string? Company { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public string? Country { get; init; }

    public string? VatId { get; init; }
}

/// <summary>Konaklamaya ait opsiyonel beyanlar.</summary>
public sealed record PublicStayRequest
{
    /// <summary>Tahmini geliş saati (otel yerel saati, <c>HH:mm</c>).</summary>
    public TimeOnly? EstimatedArrivalLocalTime { get; init; }

    /// <summary>Serbest metin not (≤ 500). Özel kategori veri istenmez; form etiketinde uyarı olur.</summary>
    public string? GuestNote { get; init; }
}

/// <summary>Ödeme tercihi. Bu fazda yalnızca "girişte ödeme" ve <c>guarantee: null</c>.</summary>
public sealed record PublicPaymentRequest
{
    public string Method { get; init; } = PublicPaymentOptions.PayAtPropertyMethod;

    /// <summary>Bu fazda <b>yalnızca <c>null</c></b>; aksi hâlde 400 <c>CHANNEL_NOT_CONFIGURED</c>.</summary>
    public string? Guarantee { get; init; }
}

/// <summary>
/// Sözleşmesel rızalar — <b>çerez onayı değildir</b> (§25 TDDDG istemci tarafındadır ve API
/// hiçbir çerez koymaz). Versiyonlar DSGVO Art. 7 Abs. 1 (hesap verebilirlik) gereği kaydedilir.
/// </summary>
public sealed record PublicConsentsRequest
{
    public bool TermsAccepted { get; init; }

    public string? TermsVersion { get; init; }

    public bool PrivacyNoticeAcknowledged { get; init; }

    public string? PrivacyNoticeVersion { get; init; }

    /// <summary>§312g Abs. 2 Nr. 9 — cayma hakkının <b>bulunmadığı</b> bildiriminin okunduğu beyanı.</summary>
    public bool WithdrawalNoticeAcknowledged { get; init; }

    public string? WithdrawalNoticeVersion { get; init; }

    /// <summary>18+ beyanı (§§104 ff. BGB — hukuki değeri sınırlıdır, kanıt olarak tutulur).</summary>
    public bool BookerIsAdult { get; init; }

    /// <summary>
    /// Pazarlama izni. <b>Ön işaretli olamaz</b> (DSGVO Art. 4 Nr. 11): varsayılan <c>false</c>
    /// ve rezervasyon <c>false</c> ile de tamamlanır.
    /// </summary>
    public bool MarketingOptIn { get; init; }
}

/// <summary>
/// <c>POST /api/v1/public/hotels/{hotelSlug}/bookings</c>.
///
/// <para><b>Kart alanı YOKTUR ve eklenmeyecektir</b> (architecture-public-booking.md §6.2).
/// Gövdede <c>cardNumber</c>, <c>pan</c>, <c>cvc</c>, <c>cvv</c>, <c>expiryMonth</c>,
/// <c>expiryYear</c>, <c>cardholderName</c> adlarından biri geçerse istek middleware seviyesinde
/// <b>400 <c>CARD_DATA_NOT_ACCEPTED</c></b> ile reddedilir ve gövde <b>loglanmaz</b>. Amaç, iyi
/// niyetli bir geliştiricinin "geçici olarak" kart alanı eklemesini imkânsız kılmaktır: bir kez
/// PAN kabul etmek tüm API'yi, log altyapısını ve yedekleri PCI-DSS kapsamına sokar.</para>
///
/// <para><b>Kişi sayısı ve tarihler istekten OKUNMAZ</b>, hold'dan alınır: istemcinin araya
/// girip fiyatı etkileyen bir değeri değiştirmesi mümkün değildir.</para>
/// </summary>
public sealed record PublicCreateBookingRequest : IRequest<PublicBookingResponse>
{
    public string HoldToken { get; init; } = string.Empty;

    public PublicCheckoutRequest Checkout { get; init; } = new();

    public PublicGuestRequest Guest { get; init; } = new();

    /// <summary>Opsiyonel blok; yalnızca kurumsal fatura isteyen misafir doldurur.</summary>
    public PublicInvoiceAddressRequest? InvoiceAddress { get; init; }

    public PublicStayRequest Stay { get; init; } = new();

    public PublicPaymentRequest Payment { get; init; } = new();

    public PublicConsentsRequest Consents { get; init; } = new();

    /// <summary>Bot koruması sağlayıcısının opak değeri (bu fazda <c>null</c>).</summary>
    public string? ChallengeToken { get; init; }

    /// <summary>Controller doldurur; gövdeden okunmaz ve yanıta yazılmaz.</summary>
    [JsonIgnore]
    public string? ClientIp { get; init; }
}
