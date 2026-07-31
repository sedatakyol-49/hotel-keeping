using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// 15 dakikalık geçici tutmanın (hold) yaşam döngüsü: oluşturma, okuma, bırakma, tüketme.
///
/// <para><b>Süre 15 dakikadır ve otel bazında ayarlanamaz</b> (architecture-public-booking.md
/// §5.2): form doldurma süresi (ölçülen sektör ortalaması 4–6 dk) ile botların envanteri park
/// etme maliyeti arasındaki denge. Uzatma yoktur — misafir geri gidip yeniden teklif alırsa yeni
/// bir hold oluşur (oda hâlâ boşsa <b>aynı</b> oda seçilir, çünkü seçim deterministiktir).
/// Tek davranış, tek test.</para>
///
/// <para><b>Kişisel veri yazılmaz:</b> hold aşamasında misafir henüz hiçbir şey beyan etmemiştir
/// (DSGVO Art. 5 Abs. 1 lit. c). Tek kimlik izi <b>tuzlanmış</b> IP özetidir ve yalnızca kötüye
/// kullanım analizi içindir.</para>
/// </summary>
internal sealed class PublicHoldService(
    IAppDbContext database,
    IDateTimeProvider clock,
    PublicPricingService pricing,
    PublicAvailabilityReader availability)
{
    /// <summary>Hold süresi — sabit, otel ayarı değildir.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    /// <summary>Rezervasyon formundaki <b>zorunlu</b> alanlar (veri minimizasyonu).</summary>
    public static readonly IReadOnlyList<string> RequiredGuestFields = ["firstName", "lastName", "email"];

    /// <summary>Opsiyonel alanlar; doğum tarihi/uyrukluk/kimlik <b>hiç sorulmaz</b> (Meldeschein).</summary>
    public static readonly IReadOnlyList<string> OptionalGuestFields =
        ["phone", "invoiceAddress", "estimatedArrivalLocalTime", "guestNote"];

    /// <summary>
    /// Hold oluşturur. Sıra sözleşmedeki (§5.1) davranışın birebir aynısıdır:
    /// <list type="number">
    ///   <item>aynı transaction'da, ilgili oda tipi + kesişen aralık için <b>süresi dolmuş</b>
    ///   hold'lar fiziksel olarak silinir (kısıt predikatı zaman ifadesi içeremediği için),</item>
    ///   <item>uygun odalar arasından <b>deterministik</b> seçim,</item>
    ///   <item>teklif dondurulur ve satır yazılır — çakışmayı
    ///   <c>EX_BookingHolds_NoOverlappingActiveHolds</c> çözer.</item>
    /// </list>
    /// </summary>
    public async Task<PublicHoldCreation> CreateAsync(
        PublicHotelContext hotel,
        PublicRoomTypeRow roomType,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int children,
        string culture,
        string? clientIpHash,
        IReadOnlyDictionary<string, string> legalVersions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(roomType);

        var now = clock.UtcNow;

        await PurgeExpiredAsync(roomType.Id, checkIn, checkOut, now, cancellationToken).ConfigureAwait(false);

        var candidates = await availability
            .GetAvailableRoomsAsync(checkIn, checkOut, now, roomType.Id, cancellationToken)
            .ConfigureAwait(false);

        var room = candidates.Count > 0
            ? candidates[0]
            : throw PublicApiException.Conflict(
                PublicErrorCodes.RoomNoLongerAvailable,
                Messages.PublicRoomNoLongerAvailable);

        var price = await pricing
            .BuildAsync(hotel, roomType.Id, roomType.BasePrice, checkIn, checkOut, adults, children, cancellationToken)
            .ConfigureAwait(false);

        var policy = PublicCancellationService.Build(hotel, checkIn, price.AccommodationGross, now);
        var legal = PublicLegalReader.BuildNotices(hotel, legalVersions);
        var summary = BuildOrderSummary(hotel, roomType, price, checkIn, checkOut, adults, children);

        var token = PublicTokens.NewHoldToken();
        var expiresAt = now + Duration;

        var hold = new BookingHold
        {
            HotelId = hotel.HotelId,
            RoomTypeId = roomType.Id,
            RoomId = room.RoomId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Adults = adults,
            Children = children,
            TokenHash = PublicTokens.Hash(token),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            ClientIpHash = clientIpHash,
            Currency = price.Currency,
            AccommodationGross = price.AccommodationGross,
            CityTaxAmount = price.CityTax.Amount,
            TotalGross = price.TotalGross,
            PriceSnapshotJson = PublicJson.Serialize(price),
            CancellationPolicySnapshotJson = PublicJson.Serialize(policy),
            OrderSummaryJson = PublicJson.Serialize(summary),
            SummaryHash = summary.Hash,
            LegalSnapshotJson = PublicJson.Serialize(legal),
            Culture = culture
        };

        database.BookingHolds.Add(hold);

        // Çakışma kısıtı ihlali (23P01) AppDbContext tarafından ConflictException'a çevrilir;
        // public sözleşme bunu ROOM_NO_LONGER_AVAILABLE olarak bildirmelidir.
        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictException exception)
        {
            throw new PublicApiException(
                409,
                PublicErrorCodes.RoomNoLongerAvailable,
                Messages.PublicRoomNoLongerAvailable,
                innerException: exception);
        }

        return new PublicHoldCreation(hold, token, BuildResponse(hotel, hold, roomType.Code, token, now));
    }

    /// <summary>
    /// Token'dan hold'u bulur. <b>Tenant filtresi işi yapar:</b> başka otelin token'ı bu otelin
    /// yolunda sunulursa satır filtreye takılır ve <c>404</c> döner — ayrı bir otel kontrolü
    /// yazmaya gerek yoktur (architecture-public-booking.md §4.2).
    /// </summary>
    public async Task<BookingHold?> FindAsync(string token, CancellationToken cancellationToken)
    {
        var hash = PublicTokens.Hash(token);

        return await database.BookingHolds
            .FirstOrDefaultAsync(hold => hold.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Donmuş hold'dan yanıtı yeniden kurar. <b>Yeni bir teklif hesaplanmaz</b>: sayfa
    /// yenilendiğinde misafirin gördüğü fiyat değişmemelidir (§312j Abs. 2).
    /// </summary>
    public PublicHoldResponse BuildFromSnapshot(
        PublicHotelContext hotel,
        BookingHold hold,
        string roomTypeCode,
        string? rawToken)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(hold);

        return BuildResponse(hotel, hold, roomTypeCode, rawToken, clock.UtcNow);
    }

    /// <summary>Hold'u serbest bırakır — envanter <b>hemen</b> boşalır (fiziksel silme).</summary>
    public async Task ReleaseAsync(BookingHold hold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);

        database.BookingHolds.Remove(hold);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Süresi dolmuş hold'ları <b>fiziksel olarak</b> siler. <c>ISoftDeletable</c> olsaydı
    /// silinmiş satırlar çakışma kısıtının predikatında kalır ve odayı sonsuza dek bloke ederdi.
    /// </summary>
    public async Task PurgeExpiredAsync(
        Guid roomTypeId,
        DateOnly checkIn,
        DateOnly checkOut,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expired = await database.BookingHolds
            .Where(hold => hold.RoomTypeId == roomTypeId
                           && hold.ConsumedAt == null
                           && hold.ExpiresAt <= now
                           && hold.CheckIn < checkOut
                           && checkIn < hold.CheckOut)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return;
        }

        database.BookingHolds.RemoveRange(expired);
    }

    /// <summary>Hold'un durumunu sözleşmedeki hatalara çevirir (404 / 409).</summary>
    public void EnsureUsable(BookingHold? hold)
    {
        if (hold is null)
        {
            throw PublicApiException.NotFound(PublicErrorCodes.HoldNotFound, Messages.PublicHoldNotFound);
        }

        if (hold.ConsumedAt is not null)
        {
            // Yanıt bookingReference İÇERMEZ: sorgulama yalnızca accessToken ile yapılır.
            throw PublicApiException.Conflict(
                PublicErrorCodes.HoldAlreadyUsed,
                Messages.PublicHoldAlreadyUsed);
        }

        if (hold.ExpiresAt <= clock.UtcNow)
        {
            throw PublicApiException.Conflict(PublicErrorCodes.HoldExpired, Messages.PublicHoldExpired);
        }
    }

    /// <summary>§312j Abs. 2 BGB zorunlu özeti — <b>alan alan</b>, düz metin değil.</summary>
    public static PublicOrderSummaryResponse BuildOrderSummary(
        PublicHotelContext hotel,
        PublicRoomTypeRow roomType,
        PublicPriceResponse price,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int children)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(roomType);
        ArgumentNullException.ThrowIfNull(price);

        var nights = checkOut.DayNumber - checkIn.DayNumber;

        var components = new List<PublicOrderSummaryComponentResponse>(2)
        {
            new()
            {
                Kind = "Accommodation",
                LabelKey = "summary.accommodation",
                // İSTEĞİN DİLİNDE. Etiket hold satırına dondurulur ve §312j Abs. 2 kanıtı olarak
                // okunur; sabit İngilizce bir metin, Almanca satın alan misafirin gördüğü özetle
                // saklanan kanıtı ayrıştırırdı (ve zorunlu özet misafirin dilinde olmak zorundadır).
                // Hash bu etiketi de kapsar, ama özet DONDUĞU için sonraki okumalarda yeniden
                // hesaplanmaz — dil değiştirmek hash'i bozmaz.
                Label = Messages.PublicSummaryAccommodation(roomType.Name, nights),
                Amount = price.AccommodationGross,
                Mandatory = true
            }
        };

        if (price.CityTax.Applies)
        {
            components.Add(new PublicOrderSummaryComponentResponse
            {
                Kind = "CityTax",
                LabelKey = "summary.cityTax",
                Label = Messages.PublicSummaryCityTax(price.CityTax.TaxablePersons, price.CityTax.Nights),
                Amount = price.CityTax.Amount,
                Mandatory = true
            });
        }

        var summary = new PublicOrderSummaryResponse
        {
            EssentialFeatures = new PublicEssentialFeaturesResponse
            {
                RoomTypeName = roomType.Name,
                RoomCount = 1,
                Occupancy = new PublicOccupancyResponse { Adults = adults, Children = children },
                Board = "None"
            },
            Duration = new PublicDurationResponse
            {
                CheckIn = checkIn,
                CheckOut = checkOut,
                Nights = nights,
                CheckInFromLocal = hotel.Hotel.CheckInFromLocal,
                CheckOutUntilLocal = hotel.Hotel.CheckOutUntilLocal,
                TimeZoneId = hotel.Hotel.TimeZoneId
            },
            TotalPrice = new PublicTotalPriceResponse
            {
                Amount = price.TotalGross,
                Currency = price.Currency,
                VatIncluded = true,
                IncludesMandatoryCharges = true
            },
            Components = components
        };

        return summary with { Hash = PublicJson.ComputeSummaryHash(summary) };
    }

    private static PublicHoldResponse BuildResponse(
        PublicHotelContext hotel,
        BookingHold hold,
        string roomTypeCode,
        string? rawToken,
        DateTimeOffset now)
    {
        var remaining = hold.ExpiresAt - now;

        return new PublicHoldResponse
        {
            HoldToken = rawToken ?? string.Empty,
            ExpiresAt = hotel.ToHotelLocal(hold.ExpiresAt),
            ExpiresInSeconds = remaining > TimeSpan.Zero ? (int)remaining.TotalSeconds : 0,
            HotelSlug = hotel.Hotel.PublicSlug ?? string.Empty,
            RoomTypeCode = roomTypeCode,
            CheckIn = hold.CheckIn,
            CheckOut = hold.CheckOut,
            Nights = hold.CheckOut.DayNumber - hold.CheckIn.DayNumber,
            Adults = hold.Adults,
            Children = hold.Children,
            Price = PublicJson.Deserialize<PublicPriceResponse>(hold.PriceSnapshotJson) ?? new PublicPriceResponse(),
            CancellationPolicy =
                PublicJson.Deserialize<PublicCancellationPolicyResponse>(hold.CancellationPolicySnapshotJson)
                ?? new PublicCancellationPolicyResponse(),
            OrderSummary =
                PublicJson.Deserialize<PublicOrderSummaryResponse>(hold.OrderSummaryJson)
                ?? new PublicOrderSummaryResponse(),
            Legal =
                PublicJson.Deserialize<PublicLegalNoticesResponse>(hold.LegalSnapshotJson)
                ?? new PublicLegalNoticesResponse(),
            PaymentOptions = PublicPaymentOptions.PayAtProperty,
            RequiredGuestFields = RequiredGuestFields,
            OptionalGuestFields = OptionalGuestFields
        };
    }
}

/// <summary>Hold oluşturmanın sonucu — ham token yalnızca burada taşınır.</summary>
/// <param name="Hold">Yazılan satır (tüketilirken tekrar kullanılır).</param>
/// <param name="RawToken">Misafire dönen token; veritabanında yalnızca özeti vardır.</param>
/// <param name="Response">Sözleşme yanıtı.</param>
internal sealed record PublicHoldCreation(BookingHold Hold, string RawToken, PublicHoldResponse Response);

/// <summary>
/// Bu fazda sunulan ödeme seçenekleri. PSP takıldığında yalnızca bu liste ve
/// <c>IPaymentAuthorizationProvider</c> implementasyonu değişir; <b>DTO'lar ve uç yolları
/// değişmez</b>.
/// </summary>
internal static class PublicPaymentOptions
{
    /// <summary>Girişte ödeme; kart garantisi istenmez.</summary>
    public static readonly IReadOnlyList<PublicPaymentOptionResponse> PayAtProperty =
    [
        new() { Method = "PayAtProperty", RequiresGuarantee = false, Description = null }
    ];

    /// <summary>Sözleşmede tanınan tek yöntem adı.</summary>
    public const string PayAtPropertyMethod = "PayAtProperty";
}
