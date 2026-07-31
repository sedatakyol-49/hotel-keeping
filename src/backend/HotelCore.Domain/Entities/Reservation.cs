using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Rezervasyon.
/// <para>
/// <b>Çakışma iki katmanda korunur.</b> Uygulama katmanındaki <c>IAvailabilityService</c>
/// kullanıcıya anlamlı mesaj üretmek için <i>ön kontrol</i> yapar ((HotelId, RoomId, CheckIn,
/// CheckOut) index'i üzerinden); ancak ön kontrol kilit almaz, dolayısıyla iki eşzamanlı istek
/// birbirinin henüz commit edilmemiş satırını görmez. Tek <b>kesin</b> koruma veritabanı
/// kısıtıdır:
/// <c>EX_Reservations_NoOverlappingStays</c> —
/// <c>EXCLUDE USING gist ("RoomId" WITH =, daterange("CheckIn","CheckOut",'[)') WITH &amp;&amp;)
/// WHERE ("Status" NOT IN ('Cancelled','NoShow') AND NOT "IsDeleted")</c>.
/// İhlal SQLSTATE 23P01 üretir ve <c>AppDbContext</c> bunu 409'a çevirir.
/// </para>
/// <para>
/// <c>CK_Reservations_ValidStay</c> ayrıca <c>CheckIn &lt; CheckOut</c> garantisi verir: eşitlik
/// hâlinde <c>daterange</c> <b>boş</b> aralık üretir, boş aralık hiçbir şeyle çakışmaz ve
/// rezervasyon dışlama kısıtından sessizce kaçardı.
/// </para>
/// </summary>
public sealed class Reservation : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomId { get; set; }

    public Room Room { get; set; } = null!;

    public Guid GuestId { get; set; }

    public Guest Guest { get; set; } = null!;

    /// <summary>Fiyatın alındığı plan (opsiyonel; serbest fiyat da girilebilir).</summary>
    public Guid? RatePlanId { get; set; }

    public RatePlan? RatePlan { get; set; }

    /// <summary>Misafire iletilen okunur rezervasyon kodu (otel içinde benzersiz).</summary>
    public string ReservationNumber { get; set; } = string.Empty;

    /// <summary>Giriş günü (takvim günü — otel saat dilimi).</summary>
    public DateOnly CheckIn { get; set; }

    /// <summary>Çıkış günü (dahil değil).</summary>
    public DateOnly CheckOut { get; set; }

    public int Adults { get; set; } = 1;

    public int Children { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Option;

    public ReservationChannel Channel { get; set; } = ReservationChannel.Direct;

    /// <summary>Konaklamanın toplam brüt tutarı.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Ön ödeme yüzdesi (0-100).</summary>
    public decimal DepositPercent { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }

    public DateTimeOffset? CheckedOutAt { get; set; }

    public Folio? Folio { get; set; }

    /// <summary>
    /// Public kanaldan geldiyse misafir yüzü ve rıza anlık görüntüsü; resepsiyon
    /// rezervasyonlarında <c>null</c>.
    /// </summary>
    public PublicBooking? PublicBooking { get; set; }

    public ICollection<Invoice> Invoices { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
