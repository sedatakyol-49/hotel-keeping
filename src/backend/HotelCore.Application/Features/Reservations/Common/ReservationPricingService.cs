using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Konaklama tutarının <b>sunucuda</b> hesaplandığı tek yer. İstemciden gelen tutara asla
/// güvenilmez (fiyat manipülasyonu); <c>POST</c>, <c>PUT</c> ve <b>misafir kanalının teklifi</b>
/// aynı hesabı kullanır.
/// <para>
/// <b>Hesap:</b> gece sayısı = <c>CheckOut - CheckIn</c> (yarı açık aralık: çıkış günü için
/// ücret alınmaz). Her gece için o geceye ait fiyat bulunur ve toplanır — böylece sezon
/// geçişinde (plan sınırında) doğru tutar oluşur, tek bir plan tüm konaklamaya uygulanmaz.
/// </para>
/// <para>
/// <b>Fiyat seçim önceliği (bir gece için):</b>
/// <list type="number">
///   <item>o geceyi kapsayan, <b>rezervasyonun kanalına özel</b> aktif plan
///         (<c>RatePlan.Channel == reservation.Channel</c>),</item>
///   <item>o geceyi kapsayan, <b>tüm kanallar</b> için aktif plan (<c>Channel == null</c>),</item>
///   <item>plan yoksa oda tipinin <c>BasePrice</c>'ı.</item>
/// </list>
/// Aynı <c>(RoomTypeId, Channel)</c> için çakışan aktif plan oluşturulması engellendiği için
/// (bkz. <c>RatePlanReader.EnsureNoOverlapAsync</c>) her adımda en fazla bir aday bulunur;
/// yine de determinizm için <c>ValidFrom</c> sırası uygulanır.
/// </para>
/// <para>
/// <b>İki giriş, tek hesap</b> (architecture-public-booking.md §8): asıl hesabı
/// <see cref="CalculateForRoomTypeAsync"/> yapar; <see cref="CalculateAsync"/> odadan oda tipini
/// çözüp ona <b>delege eder</b>. Misafir kanalı somut odayı ancak hold anında seçtiği için oda
/// tipi bazlı girişe ihtiyaç duyar — <b>ikinci bir fiyat motoru yazılmaz</b>.
/// </para>
/// </summary>
internal sealed class ReservationPricingService(IAppDbContext database)
{
    /// <summary>Tek istekte hesaplanabilecek en uzun konaklama (gece).</summary>
    public const int MaxNights = 365;

    /// <summary>
    /// Konaklama tutarını ve kullanılan fiyat planını döner.
    /// </summary>
    /// <param name="roomId">Oda (oda tipi ve otel buradan çözülür).</param>
    /// <param name="checkIn">Giriş günü (dahil).</param>
    /// <param name="checkOut">Çıkış günü (dahil değil).</param>
    /// <param name="channel">Rezervasyon kanalı — kanal bazlı plan seçimi için.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>
    /// Toplam brüt tutar ve <b>ilk gecenin</b> fiyat planı kimliği. Konaklama birden çok planı
    /// kapsıyorsa raporlamada "hangi planla satıldı" sorusunun cevabı geliş gecesidir; tutar
    /// yine de gece gece hesaplanmış toplamdır.
    /// </returns>
    public async Task<ReservationPricing> CalculateAsync(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationChannel channel,
        CancellationToken cancellationToken)
    {
        var room = await database.Rooms
            .Where(candidate => candidate.Id == roomId)
            .Select(candidate => new { candidate.RoomTypeId, candidate.RoomType.BasePrice })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), roomId);

        return await CalculateForRoomTypeAsync(
                room.RoomTypeId,
                room.BasePrice,
                checkIn,
                checkOut,
                channel,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asıl hesap: verilen <b>oda tipi</b> için gece gece fiyatlama.
    /// </summary>
    /// <param name="roomTypeId">Oda tipi.</param>
    /// <param name="basePrice">Oda tipinin liste fiyatı (plan bulunamayan geceler için).</param>
    /// <param name="checkIn">Giriş günü (dahil).</param>
    /// <param name="checkOut">Çıkış günü (dahil değil).</param>
    /// <param name="channel">Kanal — <c>Website</c> planı yoksa "tüm kanallar" planına düşülür.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public async Task<ReservationPricing> CalculateForRoomTypeAsync(
        Guid roomTypeId,
        decimal basePrice,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationChannel channel,
        CancellationToken cancellationToken)
    {
        var nights = checkOut.DayNumber - checkIn.DayNumber;
        if (nights is <= 0 or > MaxNights)
        {
            // Validator bu durumu zaten yakalar; burada savunma amaçlı bir guard var.
            throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["CheckOut"] = [Messages.StayNightsRange(MaxNights)]
            });
        }

        var lastNight = checkOut.AddDays(-1);

        // Adaylar TEK sorguda alınır (gece başına sorgu = N+1). Filtre: oda tipi, aktif,
        // konaklama aralığıyla kesişen geçerlilik ve ilgili kanal (ya da tüm kanallar).
        var candidates = await database.RatePlans
            .Where(plan => plan.RoomTypeId == roomTypeId
                           && plan.IsActive
                           && plan.ValidFrom <= lastNight
                           && checkIn <= plan.ValidTo
                           && (plan.Channel == null || plan.Channel == channel))
            .OrderBy(plan => plan.ValidFrom)
            .ThenBy(plan => plan.Id)
            .Select(plan => new RatePlanCandidate(
                plan.Id,
                plan.Price,
                plan.ValidFrom,
                plan.ValidTo,
                plan.Channel))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = 0m;
        Guid? firstNightPlanId = null;
        var nightly = new List<NightlyRate>(nights);

        for (var offset = 0; offset < nights; offset++)
        {
            var night = checkIn.AddDays(offset);
            var plan = SelectPlanForNight(candidates, night, channel);
            var price = plan?.Price ?? basePrice;

            total += price;
            nightly.Add(new NightlyRate(night, price));

            if (offset == 0)
            {
                firstNightPlanId = plan?.Id;
            }
        }

        return new ReservationPricing(
            Math.Round(total, 2, MidpointRounding.AwayFromZero),
            nights,
            firstNightPlanId,
            nightly);
    }

    /// <summary>
    /// Bir gece için geçerli planı seçer: önce kanala özel, yoksa "tüm kanallar" planı.
    /// </summary>
    private static RatePlanCandidate? SelectPlanForNight(
        List<RatePlanCandidate> candidates,
        DateOnly night,
        ReservationChannel channel)
    {
        RatePlanCandidate? fallback = null;

        foreach (var candidate in candidates)
        {
            if (candidate.ValidFrom > night || night > candidate.ValidTo)
            {
                continue;
            }

            if (candidate.Channel == channel)
            {
                // Kanala özel plan her zaman kazanır; aramayı sürdürmenin anlamı yok.
                return candidate;
            }

            fallback ??= candidate;
        }

        return fallback;
    }

    private sealed record RatePlanCandidate(
        Guid Id,
        decimal Price,
        DateOnly ValidFrom,
        DateOnly ValidTo,
        ReservationChannel? Channel);
}

/// <summary>Tek gecenin brüt fiyatı — sezon geçişinde geceler farklı olabilir.</summary>
/// <param name="Date">Konaklama gecesi (giriş günü dahil, çıkış günü hariç).</param>
/// <param name="Gross">O gecenin brüt (KDV dâhil) fiyatı.</param>
internal sealed record NightlyRate(DateOnly Date, decimal Gross);

/// <summary>Sunucuda hesaplanan fiyat sonucu.</summary>
/// <param name="TotalAmount">Konaklamanın toplam brüt tutarı.</param>
/// <param name="Nights">Gece sayısı (<c>CheckOut - CheckIn</c>).</param>
/// <param name="RatePlanId">İlk gecenin fiyat planı; plan yoksa <c>null</c> (BasePrice kullanıldı).</param>
/// <param name="Nightly">
/// Gece gece kırılım. <b>Neden döndürülüyor:</b> PAngV gereği misafire "gecelik X €" tek bir
/// sayıyla gösterilemez (sezon geçişinde yanıltıcı olur); sözleşme <c>nightly[]</c> dizisini ve
/// ayrıca etiketlenmiş bir ortalamayı ister. Toplamı yeniden bölmek kuruş kaçağı üretirdi.
/// </param>
internal sealed record ReservationPricing(
    decimal TotalAmount,
    int Nights,
    Guid? RatePlanId,
    IReadOnlyList<NightlyRate> Nightly);
