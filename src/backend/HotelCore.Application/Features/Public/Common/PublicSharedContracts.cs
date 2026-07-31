namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Misafire açık kanalın <b>paylaşılan</b> yanıt parçaları.
///
/// <para><b>Neden admin DTO'larından tamamen ayrı</b> (architecture-public-booking.md §4.3):
/// admin DTO'ları zamanla büyür — <c>RoomTypeResponse</c>'a yarın maliyet, doluluk veya iç not
/// eklenir. Paylaşılan bir tip o alanı <b>sessizce</b> public yanıta taşır; kimse bir güvenlik
/// kararı vermediği hâlde veri sızar. Ayrılık, sızıntıyı bir <i>unutma</i> hatasından bir
/// <i>bilinçli ekleme</i> hatasına dönüştürür.</para>
///
/// <para><b>Public yanıtta bulunması YASAK alanlar:</b> oda numarası · kat ·
/// <c>housekeepingStatus</c> · <c>isOutOfOrder</c> · oda/oda tipi iç notu · <c>roomId</c> ·
/// <c>roomTypeId</c> (GUID) · başka misafirlerin adı/e-postası · rezervasyon sayıları, doluluk,
/// ADR/RevPAR · maliyet · fatura/folio verisi · <c>reservationNumber</c> · <c>ratePlanId</c>/plan
/// adı · <c>Reservation.Notes</c> · personel bilgisi · <c>HotelId</c>/<c>HeadOfficeId</c>
/// GUID'leri. Kimlikler public tarafta <b>stabil metin anahtarlarıdır</b>: otel →
/// <c>hotelSlug</c>, oda tipi → <c>roomTypeCode</c>, rezervasyon → <c>bookingReference</c>.</para>
/// </summary>
public sealed record PublicImageResponse
{
    public string Url { get; init; } = string.Empty;

    /// <summary>Erişilebilirlik alt metni (WCAG 1.1.1) — isteğin dilinde çözülmüş.</summary>
    public string? Alt { get; init; }

    /// <summary>Genişlik/yükseklik <b>işaretlemeye</b> yazılabilsin diye döner (CLS önlemi).</summary>
    public int? Width { get; init; }

    public int? Height { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>Bir konaklama gecesinin brüt fiyatı.</summary>
public sealed record PublicNightlyRateResponse
{
    /// <summary>Konaklama gecesi (<c>yyyy-MM-dd</c>).</summary>
    public DateOnly Date { get; init; }

    public decimal Gross { get; init; }
}

/// <summary>
/// Kurtaxe bileşeni. <b>Toplama dâhildir</b> (PAngV: Gesamtpreis tüm zorunlu bileşenleri içerir)
/// ama <b>ayrı</b> gösterilir ve <see cref="ChargedOnlyIfStayTakesPlace"/> bayrağını taşır:
/// konaklama gerçekleşmezse vergi doğmaz (<c>CityTaxLiability.ArisesFrom</c> ile aynı kural).
/// </summary>
public sealed record PublicCityTaxResponse
{
    public bool Applies { get; init; }

    public decimal Amount { get; init; }

    public decimal PerPersonNight { get; init; }

    /// <summary>Vergiye tabi kişi sayısı — <c>TaxProfile.CountTaxablePersons</c> ile hesaplanır.</summary>
    public int TaxablePersons { get; init; }

    public int Nights { get; init; }

    /// <summary>Kurtaxe KDV dışıdır (<i>durchlaufender Posten</i>, UStG §10 Abs. 1 Satz 5).</summary>
    public decimal VatRate { get; init; }

    public bool IncludedInTotal { get; init; } = true;

    public bool ChargedOnlyIfStayTakesPlace { get; init; } = true;

    public bool ChildExemptionApplied { get; init; }

    /// <summary>Bilgilendirmedir; <b>hesaba girmez</b> (rezervasyonda doğum tarihi tutulmaz).</summary>
    public int? ChildAgeLimit { get; init; }
}

/// <summary>
/// PAngV fiyat nesnesi — <b>fiyat taşıyan tüm yanıtlarda aynı şekil</b>.
/// <para>
/// Değişmezler (testle korunur): <c>accommodationNet + accommodationVat == accommodationGross</c>,
/// <c>totalGross == accommodationGross + cityTax.amount</c>,
/// <c>sum(nightly[].gross) == accommodationGross</c>,
/// <c>cityTax.amount == taxablePersons × nights × perPersonNight</c> ve — en önemlisi —
/// <c>totalGross</c>, aynı rezervasyondan üretilen faturanın <c>grossAmount</c>'una
/// <b>kuruşu kuruşuna</b> eşittir.
/// </para>
/// </summary>
public sealed record PublicPriceResponse
{
    public string Currency { get; init; } = "EUR";

    /// <summary>Gesamtpreis = konaklama + <b>tüm</b> zorunlu kalemler.</summary>
    public decimal TotalGross { get; init; }

    public bool VatIncluded { get; init; } = true;

    public bool MandatoryExtrasIncluded { get; init; } = true;

    public decimal AccommodationGross { get; init; }

    public decimal AccommodationNet { get; init; }

    public decimal AccommodationVat { get; init; }

    /// <summary>Otelin <b>indirimli</b> oranı (<c>TaxProfile.ReducedVatRate</c>) — konaklama hizmeti.</summary>
    public decimal AccommodationVatRate { get; init; }

    public PublicCityTaxResponse CityTax { get; init; } = new();

    public IReadOnlyList<PublicNightlyRateResponse> Nightly { get; init; } = [];

    /// <summary>
    /// Yalnızca <b>gösterim ortalamasıdır</b>. Geceler eşit değilse istemci bunu ortalama olarak
    /// etiketlemek <b>zorundadır</b> (PAngV: yanıltıcı fiyat gösterimi yasağı).
    /// </summary>
    public decimal AverageNightlyGross { get; init; }

    /// <summary>Girişte ödeme → ön ödeme yok.</summary>
    public decimal DepositPercent { get; init; }

    public decimal AmountDueAtProperty { get; init; }

    public decimal PrepaidAmount { get; init; }

    /// <summary>Bu fazda her zaman boş (ekstralar public tarafta satılmaz).</summary>
    public IReadOnlyList<PublicOptionalExtraResponse> OptionalExtras { get; init; } = [];
}

/// <summary>Opsiyonel ek hizmet — bu fazda hiç üretilmez, sözleşme şekli korunur.</summary>
public sealed record PublicOptionalExtraResponse
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public decimal Gross { get; init; }
}

/// <summary>
/// İptal politikası. <b>Ücret matrahı yalnızca konaklama tutarıdır</b>; Kurtaxe girmez —
/// konaklama gerçekleşmediği için vergi hiç doğmaz.
/// </summary>
public sealed record PublicCancellationPolicyResponse
{
    /// <summary><c>Flexible</c> | <c>Restricted</c>.</summary>
    public string Type { get; init; } = "Flexible";

    /// <summary>Ücretsiz iptalin son anı — <b>mutlak</b>, otel yerel offset'iyle.</summary>
    public DateTimeOffset FreeCancellationUntil { get; init; }

    /// <summary>Şimdi iptal edilirse ücretsiz mi.</summary>
    public bool IsFreeCancellationAvailable { get; init; }

    public decimal LateCancellationFeePercent { get; init; }

    /// <summary><c>accommodationGross</c> üzerinden; Kurtaxe <b>hariç</b>.</summary>
    public decimal LateCancellationFeeAmount { get; init; }

    public decimal NoShowFeePercent { get; init; }

    public decimal NoShowFeeAmount { get; init; }

    /// <summary>İptal/no-show'da Kurtaxe doğmaz — <c>CityTaxLiability</c> ile aynı kural.</summary>
    public bool CityTaxRefundedOnCancellation { get; init; } = true;

    public string PolicyTextKey { get; init; } = string.Empty;
}

/// <summary>Ödeme seçeneği. Bu fazda yalnızca "girişte ödeme" döner.</summary>
public sealed record PublicPaymentOptionResponse
{
    public string Method { get; init; } = "PayAtProperty";

    public bool RequiresGuarantee { get; init; }

    public string? Description { get; init; }
}

/// <summary>§312g Abs. 2 Nr. 9 BGB — tarihli konaklamada yasal cayma hakkı <b>yoktur</b>.</summary>
public sealed record PublicWithdrawalRightResponse
{
    /// <summary>Her zaman <c>false</c>; var olmayan bir hakkı anlatmak yanıltıcı olurdu.</summary>
    public bool Applies { get; init; }

    public string LegalBasis { get; init; } = "BGB §312g Abs. 2 Nr. 9";

    public string NoticeKey { get; init; } = "legal.withdrawal.excluded.accommodation";

    public string? NoticeVersion { get; init; }
}

/// <summary>
/// §312j Abs. 3 BGB — Button-Lösung. Sunucu istemci ekranını göremez, bu yüzden metni
/// <b>doğrulamaz</b>; yapabileceği tek şey beklenen etiketi bildirmek ve gösterileni kaydetmektir.
/// </summary>
public sealed record PublicOrderButtonResponse
{
    public string LabelKey { get; init; } = "legal.orderButton.payable";

    public string LabelDe { get; init; } = "zahlungspflichtig buchen";

    /// <summary>Otel bazında değiştirilemez.</summary>
    public bool MustBeExactLabel { get; init; } = true;
}

/// <summary>Hukuki belgenin kimliği: anahtar + <b>versiyon</b> (rızada aynen kullanılır).</summary>
public sealed record PublicLegalDocumentRefResponse
{
    public string Key { get; init; } = string.Empty;

    public string? Version { get; init; }
}

/// <summary>Hold ve rezervasyon yanıtlarındaki <c>legal</c> bloğu (dondurulur).</summary>
public sealed record PublicLegalNoticesResponse
{
    public PublicWithdrawalRightResponse WithdrawalRight { get; init; } = new();

    public PublicOrderButtonResponse OrderButton { get; init; } = new();

    public PublicLegalDocumentRefResponse Terms { get; init; } = new() { Key = "terms" };

    public PublicLegalDocumentRefResponse PrivacyNotice { get; init; } = new() { Key = "privacy" };

    /// <summary><c>OnConfirmationEmail</c> | <c>OnHotelAcceptance</c>.</summary>
    public string ContractConclusion { get; init; } = "OnConfirmationEmail";
}
