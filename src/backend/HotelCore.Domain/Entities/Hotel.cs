using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Fiziksel otel / şube — multi-tenant modelinde <b>tenant kökü</b>.
/// Kendisi <see cref="ITenantEntity"/> DEĞİLDİR; erişim <see cref="UserHotelAccess"/> ile yönetilir.
/// </summary>
public sealed class Hotel : EntityBase, IAuditableEntity, ISoftDeletable
{
    public Guid HeadOfficeId { get; set; }

    public HeadOffice HeadOffice { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public Country Country { get; set; } = Country.DE;

    public string City { get; set; } = string.Empty;

    public string? AddressLine { get; set; }

    public string? PostalCode { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// <b>Steuernummer</b> (yerel vergi dairesi numarası) — fatura üstbilgisinde basılır.
    /// USt-IdNr. için ayrı <see cref="VatId"/> kolonu vardır; ikisi farklı numaralardır ve
    /// Impressum (§5 DDG) ile AB içi fatura (§14a UStG) <b>USt-IdNr.</b> ister.
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// <b>USt-IdNr.</b> (AB KDV kimlik numarası, örn. <c>DE123456789</c>).
    /// <para>
    /// <b>Neden <see cref="TaxNumber"/>'dan ayrı:</b> Almanya'da Steuernummer ve USt-IdNr. iki
    /// ayrı numaradır; §5 Abs. 1 Nr. 6 DDG künyede <i>USt-IdNr.</i> arar, sınır ötesi faturada
    /// yine USt-IdNr. gerekir. Tek kolonda tutulduğunda hangi numaranın yazıldığı belirsizleşir
    /// ve künye/fatura yanlış numarayı basar.
    /// </para>
    /// </summary>
    public string? VatId { get; set; }

    public string DefaultCulture { get; set; } = "de";

    /// <summary>ISO 4217 para birimi kodu (EUR, TRY, CHF ...).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// IANA saat dilimi kimliği (örn. <c>Europe/Berlin</c>).
    /// <para>
    /// <b>Neden zorunlu:</b> "otelin bugünü", ücretsiz iptal son tarihinin <b>mutlak</b> anı ve
    /// misafire gösterilen yerel saatler ancak saat dilimiyle hesaplanabilir. Sunucunun saat
    /// dilimini kullanmak, sunucu başka bölgeye taşındığında iptal politikasını sessizce
    /// kaydırırdı. Windows kimlikleri (<c>W. Europe Standard Time</c>) DEĞİL, IANA kimlikleri
    /// saklanır: taşınabilir ve <c>TimeZoneInfo.FindSystemTimeZoneById</c> her iki platformda da
    /// IANA kimliğini çözer.
    /// </para>
    /// </summary>
    public string TimeZoneId { get; set; } = "Europe/Berlin";

    /// <summary>Girişin başladığı yerel saat — §312j Abs. 2 BGB "süre" bilgisinin parçası.</summary>
    public TimeOnly CheckInFromLocal { get; set; } = new(15, 0);

    /// <summary>Çıkışın son yerel saati — §312j Abs. 2 BGB "süre" bilgisinin parçası.</summary>
    public TimeOnly CheckOutUntilLocal { get; set; } = new(11, 0);

    /// <summary>
    /// Otel donanım listesi — virgülle ayrılmış anahtarlar (<c>wifi,parking,breakfast</c>).
    /// <para>
    /// <b>Neden ayrı bir <c>Amenity</c> entity'si değil:</b> mevcut
    /// <see cref="RoomType.Amenities"/> zaten bu biçimi kullanıyor. İki farklı gösterim
    /// (biri normalize tablo, diğeri CSV) aynı kavram için iki farklı doğrulama, iki farklı
    /// çeviri yolu ve iki farklı public DTO üretirdi. Anahtarlar i18n katalog anahtarıdır;
    /// serbest metin değildir, bu yüzden çeviri tablosu da gerekmez.
    /// </para>
    /// </summary>
    public string? Amenities { get; set; }

    /// <summary>
    /// Misafir sitesindeki otel URL anahtarı (küçük harf, <c>a-z0-9-</c>, 3–60).
    /// Canlı satırlar arasında <b>global</b> benzersizdir — URL uzayı globaldir, marka bazında
    /// değil. Kanal kapalıyken <c>null</c> olabilir.
    /// </summary>
    public string? PublicSlug { get; set; }

    /// <summary>
    /// Otelin kendi alan adı (opsiyonel). <b>Birincil mekanizma değildir:</b> otorite yoldaki
    /// slug'dadır; bu kolon yalnızca edge/SSR katmanının host → slug çevirisi içindir
    /// (architecture-public-booking.md §4.1).
    /// </summary>
    public string? PublicHost { get; set; }

    /// <summary>Owned type — bkz. <see cref="TaxProfile"/>.</summary>
    public TaxProfile TaxProfile { get; set; } = new();

    /// <summary>Owned type — public kanal ayarları.</summary>
    public PublicBookingSettings PublicBookingSettings { get; set; } = new();

    /// <summary>Owned type — iptal politikası.</summary>
    public CancellationPolicy CancellationPolicy { get; set; } = new();

    /// <summary>Owned type — §5 DDG Impressum künyesi.</summary>
    public HotelLegalProfile LegalProfile { get; set; } = new();

    public ICollection<Department> Departments { get; } = [];

    public ICollection<RoomType> RoomTypes { get; } = [];

    public ICollection<Room> Rooms { get; } = [];

    /// <summary>Otel tanıtım görselleri (misafir sitesi galerisi).</summary>
    public ICollection<HotelImage> Images { get; } = [];

    /// <summary>Yayımlanmış hukuki belgeler (AGB, Datenschutzerklärung ...).</summary>
    public ICollection<HotelLegalDocument> LegalDocuments { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
