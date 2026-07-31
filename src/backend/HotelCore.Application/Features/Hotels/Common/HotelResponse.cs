using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Hotels.Common;

/// <summary>
/// Otelin vergi profili — api-contracts.md → "Hotels &amp; Ayarlar".
/// <para>
/// Oranlar <b>koda hardcode edilmez</b> (architecture.md §4.1); otel bazında yönetilir ve
/// faturalama bu değerleri okur.
/// </para>
/// </summary>
public sealed record TaxProfileDto
{
    /// <summary>Standart KDV oranı, yüzde (DE: 19).</summary>
    public decimal VatRate { get; init; }

    /// <summary>İndirimli KDV oranı, yüzde (DE: 7 — konaklama).</summary>
    public decimal ReducedVatRate { get; init; }

    /// <summary>Kurtaxe: kişi başı / gece şehir vergisi tutarı.</summary>
    public decimal CityTaxPerPersonNight { get; init; }

    public bool CityTaxEnabled { get; init; }

    /// <summary>
    /// Kurtaxe hesabında <b>çocuklar muaf mı</b>. <c>true</c> ise faturada vergiye tabi kişi
    /// sayısı yalnızca <c>adults</c>'tır (<c>adults + children</c> değil) — yani çocuklu
    /// rezervasyonlarda Kurtaxe tutarı düşer. Varsayılan <c>false</c> (opt-in).
    /// </summary>
    public bool CityTaxExemptChildren { get; init; }

    /// <summary>
    /// Muafiyetin geçerli olduğu yaş sınırı (DE'de tipik 18; belediyeye göre 16/14/6).
    /// <b>Hesaba girmez</b> — rezervasyonda misafir doğum tarihi tutulmadığı için yaşa göre
    /// ayrıştırma yapılamaz; değer faturada muafiyetin dayanağı olarak yazdırılır ve "çocuk"
    /// tanımını belgeler. Bilinmiyorsa <c>null</c>.
    /// </summary>
    public int? CityTaxChildAgeLimit { get; init; }
}

/// <summary>
/// Misafire açık kanal ayarları — <c>GET/PUT /hotels/{id}/settings</c>
/// (api-contracts-public-booking.md §10).
/// <para>
/// <b>Bu bir ADMIN DTO'sudur ve public DTO'lardan tamamen ayrıdır.</b> Aynı kavramı iki tip
/// anlatır çünkü kitleleri farklıdır: burada slug yazılabilir ve kanal açılıp kapatılabilir;
/// public tarafta ikisi de görünmez.
/// </para>
/// </summary>
public sealed record PublicBookingSettingsDto
{
    /// <summary>
    /// Kanal açık mı. <b>Varsayılan <c>false</c> bilinçlidir:</b> kanal açmak hukuki bir eylemdir
    /// (Impressum, AGB, aydınlatma metni yayımlamak gerekir) ve bir ayar kaydının yan etkisi
    /// olarak açılmamalıdır.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Misafir sitesindeki URL anahtarı. <b>Canlı satırlar arasında global benzersizdir</b> —
    /// URL uzayı globaldir, marka bazında değil.
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>Otelin kendi alan adı (opsiyonel); otorite yine slug'dadır.</summary>
    public string? Host { get; init; }

    public int MinNights { get; init; } = 1;

    public int MaxNights { get; init; } = 30;

    public int MaxAdvanceDays { get; init; } = 365;

    /// <summary><c>0</c> = aynı gün rezervasyon serbest.</summary>
    public int MinAdvanceHours { get; init; }

    public int MaxAdults { get; init; } = 10;

    public int MaxChildren { get; init; } = 10;

    /// <summary><c>Instant</c> | <c>OnHotelAcceptance</c> (bkz. mimari §10 madde 3).</summary>
    public string ConfirmationMode { get; init; } = nameof(PublicBookingConfirmationMode.Instant);
}

/// <summary>İptal politikası — <b>otel bazında</b> (bu fazda plan bazlı politika yoktur).</summary>
public sealed record CancellationPolicyDto
{
    /// <summary><c>Flexible</c> | <c>Restricted</c>.</summary>
    public string Type { get; init; } = nameof(CancellationPolicyType.Flexible);

    public int FreeCancellationDaysBeforeArrival { get; init; } = 3;

    /// <summary>Son tarihteki kesim saati, otelin <b>yerel</b> saati.</summary>
    public TimeOnly CutoffLocalTime { get; init; } = new(18, 0);

    public decimal LateCancellationFeePercent { get; init; } = 90.00m;

    public decimal NoShowFeePercent { get; init; } = 90.00m;
}

/// <summary>§5 DDG Impressum künyesi. <b>Hiçbir alan koda gömülmez</b>; müşteri-değişkenidir.</summary>
public sealed record HotelLegalProfileDto
{
    public string? LegalEntityName { get; init; }

    public string? LegalForm { get; init; }

    public string? RepresentedBy { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    /// <summary>Ülke enum <b>adı</b>; boşsa otelin ülkesi kullanılır.</summary>
    public string? Country { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? RegisterCourt { get; init; }

    public string? RegisterNumber { get; init; }

    public string? SupervisoryAuthority { get; init; }

    /// <summary>§36 VSBG: katılmayan işletme de bunu <b>bildirmek</b> zorundadır.</summary>
    public bool ParticipatesInDisputeResolution { get; init; }

    public string? OnlineDisputeResolutionUrl { get; init; }

    public string? DisputeResolutionNotice { get; init; }
}

/// <summary>Otel listesi satırı — otel seçici ve ayarlar listesi için yeterli alanlar.</summary>
public sealed record HotelListItemResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    /// <summary>Ülke enum <b>adı</b> (string: <c>DE | AT | CH | TR</c>), sayı değil.</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>ISO 4217 para birimi kodu.</summary>
    public string Currency { get; init; } = string.Empty;

    public string DefaultCulture { get; init; } = string.Empty;

    /// <summary>Otelin silinmemiş oda sayısı.</summary>
    public int RoomCount { get; init; }
}

/// <summary>Otel detayı — api-contracts.md → "Hotels &amp; Ayarlar" ile birebir.</summary>
public sealed record HotelResponse
{
    public Guid Id { get; init; }

    public Guid HeadOfficeId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    /// <summary>Vergi numarası (DE: Steuernummer / USt-IdNr.) — fatura üstbilgisinde basılır.</summary>
    public string? TaxNumber { get; init; }

    public string DefaultCulture { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// <b>USt-IdNr.</b> (AB KDV kimlik numarası). <see cref="TaxNumber"/> (Steuernummer) ile
    /// karıştırılmamalıdır: §5 DDG künyesi ve sınır ötesi fatura <b>USt-IdNr.</b> arar.
    /// </summary>
    public string? VatId { get; init; }

    /// <summary>IANA saat dilimi kimliği (örn. <c>Europe/Berlin</c>).</summary>
    public string TimeZoneId { get; init; } = string.Empty;

    public TimeOnly CheckInFromLocal { get; init; }

    public TimeOnly CheckOutUntilLocal { get; init; }

    /// <summary>Otel donanım anahtarları (i18n katalog anahtarları).</summary>
    public IReadOnlyList<string> Amenities { get; init; } = [];

    public int RoomCount { get; init; }

    public TaxProfileDto TaxProfile { get; init; } = new();

    public PublicBookingSettingsDto PublicBooking { get; init; } = new();

    public CancellationPolicyDto CancellationPolicy { get; init; } = new();

    public HotelLegalProfileDto LegalProfile { get; init; } = new();

    /// <summary>
    /// Engelleyici olmayan uyarılar. Örn. <c>NoRatePlanForWebsiteChannel</c>: kanal açık ama
    /// otelin <c>Website</c> ya da "tüm kanallar" (<c>channel: null</c>) fiyat planı yok — web
    /// fiyatı sessizce <c>RoomType.BasePrice</c>'a düşer.
    /// <para>
    /// <b>Neden 409 değil:</b> bu geçerli bir yapılandırmadır (BasePrice gerçek bir fiyattır),
    /// ama neredeyse her zaman istenmeyen bir sonuçtur. Kaydı reddetmek oteli kilitlerdi;
    /// sessiz kalmak ise yanlış fiyatla satış yapmasına yol açardı.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
