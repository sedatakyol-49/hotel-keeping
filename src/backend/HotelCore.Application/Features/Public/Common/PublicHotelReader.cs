using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Public uçların ihtiyaç duyduğu otel anlık görüntüsü. <b>GUID dışarı sızmaz</b>: burada
/// tutulan <see cref="HotelId"/> yalnızca sunucu içi sorgular içindir, hiçbir yanıt alanına
/// yazılmaz.
/// </summary>
/// <param name="HotelId">Tenant kapsamının oteli.</param>
/// <param name="Hotel">Otel satırı (owned type'lar dâhil, takip edilmeyen kopya).</param>
/// <param name="BrandName">Marka adı (<c>HeadOffice.BrandName</c>) — hardcode edilmez.</param>
internal sealed record PublicHotelContext(Guid HotelId, Hotel Hotel, string BrandName)
{
    /// <summary>Otelin saat dilimi; kimlik geçersizse UTC'ye düşülür (istek 500 ile ölmez).</summary>
    public TimeZoneInfo TimeZone => PublicTimeZone.Resolve(Hotel.TimeZoneId);

    /// <summary>Otelin <b>yerel</b> bugünü — "geçmiş tarih" kuralı sunucunun saatine bağlanamaz.</summary>
    public DateOnly LocalToday(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, TimeZone).DateTime);

    /// <summary>Otel yerel offset'iyle mutlak an (misafirin takvimiyle karşılaştırabilmesi için).</summary>
    public DateTimeOffset ToHotelLocal(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZone);
}

/// <summary>
/// Otel bağlamının okunduğu tek yer.
/// <para>
/// <b><c>IgnoreQueryFilters()</c> KULLANILMAZ</b> (architecture-public-booking.md §4.2): otelin
/// kendisi tenant-scoped bir entity değildir (tenant <i>kökü</i>dür), ama otel üzerinden okunan
/// her şey (görsel, oda tipi, hukuki belge) tenant filtresinden geçer. Public yola tek bir bypass
/// sokmak, tasarımın en güçlü güvencesini yok ederdi.
/// </para>
/// </summary>
internal sealed class PublicHotelReader(IAppDbContext database, ITenantContext tenant)
{
    /// <summary>
    /// Aktif public otel bağlamı. Kapsam kurulmamışsa veya otel bulunamıyorsa
    /// <b>404 <c>HOTEL_NOT_FOUND</c></b> — "slug yok", "silinmiş" ve "kanal kapalı" <b>ayırt
    /// edilmez</b>; 403 dönmek otelin varlığını doğrulardı.
    /// </summary>
    public async Task<PublicHotelContext> RequireCurrentAsync(CancellationToken cancellationToken)
    {
        if (tenant.HotelId is not Guid hotelId)
        {
            throw PublicApiException.NotFound(PublicErrorCodes.HotelNotFound, Messages.PublicHotelNotFound);
        }

        var row = await database.Hotels
            .AsNoTracking()
            .Where(candidate => candidate.Id == hotelId
                                && candidate.PublicSlug != null
                                && candidate.PublicBookingSettings.IsEnabled)
            .Select(candidate => new { Hotel = candidate, candidate.HeadOffice.BrandName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            throw PublicApiException.NotFound(PublicErrorCodes.HotelNotFound, Messages.PublicHotelNotFound);
        }

        return new PublicHotelContext(hotelId, row.Hotel, row.BrandName);
    }
}

/// <summary>
/// IANA saat dilimi çözümü. <b>Neden gerekli:</b> "otelin bugünü", ücretsiz iptalin mutlak son
/// tarihi ve misafire gösterilen yerel saatler sunucunun saat dilimine bağlanamaz — sunucu başka
/// bölgeye taşındığında iptal politikası sessizce kayardı.
/// </summary>
internal static class PublicTimeZone
{
    /// <summary>
    /// Kimliği çözer; tanınmıyorsa <see cref="TimeZoneInfo.Utc"/>'ye düşer. Yapılandırma hatası
    /// bir çalışma zamanı çökmesi değil, gözlemlenebilir bir sapma olmalıdır: 500 dönen bir
    /// public site, yanlış saat dilimiyle çalışan bir siteden daha kötüdür.
    /// </summary>
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Yerel bir tarih+saati mutlak ana çevirir.
    /// <list type="bullet">
    ///   <item><b>Spring-forward</b> (var olmayan yerel saat): <b>ilk geçerli sonraki</b> an
    ///   seçilir — aksi hâlde son tarih hiç oluşmaz.</item>
    ///   <item><b>Fall-back</b> (belirsiz yerel saat): <b>daha geç</b> offset seçilir, yani
    ///   misafir lehine olan (iptal penceresi bir saat daha uzun) an.</item>
    /// </list>
    /// </summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // Saat ileri alındı: bu yerel an takvimde yoktur. Dakika dakika ilerleyerek ilk
            // geçerli ana gidilir (geçiş en fazla birkaç saattir).
            for (var offset = 1; offset <= 240; offset++)
            {
                var candidate = local.AddMinutes(offset);
                if (!zone.IsInvalidTime(candidate))
                {
                    local = candidate;
                    break;
                }
            }
        }

        if (zone.IsAmbiguousTime(local))
        {
            // Saat geri alındı: iki geçerli offset var. Daha GEÇ olanı (küçük UTC offset)
            // seçilir; misafirin ücretsiz iptal penceresi kısalmaz.
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var chosen = offsets.Min();

            return new DateTimeOffset(local, chosen);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
