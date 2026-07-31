using System.Text.Json.Nodes;

namespace HotelCore.Application.Features.Reservations.GetPublicBooking;

/// <summary>Rezervasyonda beyan edilen kurumsal fatura künyesi (opsiyonel blok).</summary>
public sealed record PublicBookingInvoiceAddressResponse
{
    public string? Company { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public string? Country { get; init; }

    public string? VatId { get; init; }
}

/// <summary>Alınan rızalar ve onaylanan metin versiyonları (DSGVO Art. 7 Abs. 1).</summary>
public sealed record PublicBookingConsentsResponse
{
    public bool TermsAccepted { get; init; }

    public string? TermsVersion { get; init; }

    public bool PrivacyNoticeAcknowledged { get; init; }

    public string? PrivacyNoticeVersion { get; init; }

    public bool WithdrawalNoticeAcknowledged { get; init; }

    public string? WithdrawalNoticeVersion { get; init; }

    public bool BookerIsAdult { get; init; }

    public bool MarketingOptIn { get; init; }

    /// <summary>Rızaların alındığı an — kanıtın zaman damgası.</summary>
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>§312f onay belgesinin kaydı.</summary>
public sealed record PublicBookingConfirmationRecordResponse
{
    public DateTimeOffset? SentAt { get; init; }

    /// <summary>Gönderilen belgenin SHA-256 özeti — "ne gönderildi" sorusunun cevabı.</summary>
    public string? DocumentHash { get; init; }

    public string? DocumentVersion { get; init; }

    public string? Culture { get; init; }
}

/// <summary>
/// <c>GET /api/v1/reservations/{id}/public-booking</c> — rezervasyonun <b>rıza ve hukuki anlık
/// görüntüsü</b> (api-contracts-public-booking.md §10).
///
/// <para><b>Bu bir ADMIN yanıtıdır</b> ve public DTO'larla hiçbir tip paylaşmaz. Amacı
/// uyuşmazlıkta otelin elindeki kanıtı göstermektir: hangi metnin hangi versiyonu onaylandı
/// (DSGVO Art. 7 Abs. 1), düğmede hangi metin gösterildi (§312j Abs. 3), düğmenin üstünde hangi
/// özet duruyordu (§312j Abs. 2), hangi fiyat ve politika taahhüt edildi.</para>
///
/// <para><b>Erişim token'ı burada da DÖNMEZ:</b> veritabanında yalnızca özeti vardır ve
/// resepsiyonun misafirin taşıyıcı kimlik bilgisine ihtiyacı yoktur. Yalnızca erişimin ne zaman
/// kapanacağı gösterilir.</para>
/// </summary>
public sealed record ReservationPublicBookingResponse
{
    public Guid ReservationId { get; init; }

    /// <summary>Misafire gösterilen referans, <c>4-4-4</c> gruplu.</summary>
    public string BookingReference { get; init; } = string.Empty;

    /// <summary>Self-servis erişimin kapandığı an; <b>veri silinmez</b> (GoBD/AO §147).</summary>
    public DateTimeOffset AccessTokenExpiresAt { get; init; }

    public string Culture { get; init; } = string.Empty;

    /// <summary>Beyan edilen <b>ikamet</b> ülkesi; uyrukluk DEĞİLDİR (Meldeschein verisi).</summary>
    public string? CountryOfResidence { get; init; }

    public TimeOnly? EstimatedArrivalLocalTime { get; init; }

    public PublicBookingInvoiceAddressResponse? InvoiceAddress { get; init; }

    public PublicBookingConsentsResponse Consents { get; init; } = new();

    /// <summary>İstemcinin gösterdiğini bildirdiği düğme metni — sunucu doğrulamaz, kaydeder.</summary>
    public string? OrderButtonLabel { get; init; }

    public string SummaryHash { get; init; } = string.Empty;

    /// <summary>§312j Abs. 2 özetinin dondurulmuş kopyası (ham JSON).</summary>
    public JsonNode? OrderSummary { get; init; }

    /// <summary>Rezervasyon anındaki fiyat nesnesinin dondurulmuş kopyası.</summary>
    public JsonNode? Price { get; init; }

    /// <summary>Rezervasyon anındaki iptal politikasının dondurulmuş kopyası.</summary>
    public JsonNode? CancellationPolicy { get; init; }

    /// <summary>Hukuki bildirimlerin ve versiyonların dondurulmuş kopyası.</summary>
    public JsonNode? Legal { get; init; }

    /// <summary><c>Instant</c> | <c>OnHotelAcceptance</c> — rezervasyon anındaki otel ayarı.</summary>
    public string ConfirmationMode { get; init; } = string.Empty;

    public PublicBookingConfirmationRecordResponse Confirmation { get; init; } = new();

    /// <summary>Misafirin <b>online</b> iptal ettiği an (resepsiyon iptali burayı doldurmaz).</summary>
    public DateTimeOffset? CancelledAt { get; init; }

    /// <summary>İptalde bildirilen ve onaylanan ücret; matrah <b>yalnızca konaklama tutarıdır</b>.</summary>
    public decimal? CancellationFeeAmount { get; init; }
}
