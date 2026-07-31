using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Konaklama kısıtlarının (tarih, süre, kişi sayısı) <b>tek</b> değerlendirme yeri
/// (api-contracts-public-booking.md §6.3).
///
/// <para><b>Neden FluentValidation validator'ı değil:</b> kuralların eşikleri otel ayarındadır
/// (<c>PublicBookingSettings</c>) ve veritabanından okunur. Validator'lar DB'ye gitmez; eşikleri
/// validator'a taşımak ya ikinci bir okuma yolu ya da koda gömülü sabitler üretirdi. Biçim
/// kuralları (zorunluluk, negatif olmayan sayı) validator'da kalır; <b>otele bağlı</b> kurallar
/// burada.</para>
///
/// <para><b>"Bugün" otelin yerel günüdür</b>, sunucunun değil: sunucu başka bölgeye taşındığında
/// misafirin aynı gün rezervasyon yapabilme hakkı sessizce kaymamalıdır.</para>
/// </summary>
internal static class PublicStayRules
{
    /// <summary>
    /// Arama (müsaitlik) kuralları. <b>Minimum gece sayısı burada hata üretmez</b>: sözleşme
    /// arama sonucunda <c>MinNightsNotMet</c> gerekçesini beklediği için o kısıt
    /// <c>unavailableRoomTypes</c> üzerinden bildirilir — arama ekranı "neden sonuç yok"
    /// sorusuna cevap verebilmelidir.
    /// </summary>
    public static void ValidateSearch(
        Hotel hotel,
        DateOnly hotelToday,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int children)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var settings = hotel.PublicBookingSettings;

        if (checkIn < hotelToday)
        {
            errors["CheckIn"] = [Messages.PublicCheckInInPast(hotelToday)];
        }

        if (checkOut <= checkIn)
        {
            errors["CheckOut"] = [Messages.CheckOutAfterCheckIn];
        }

        var nights = checkOut.DayNumber - checkIn.DayNumber;
        if (nights > settings.MaxNights)
        {
            errors["CheckOut"] = [Messages.PublicNightsRange(settings.MinNights, settings.MaxNights)];
        }

        if (checkIn > hotelToday.AddDays(settings.MaxAdvanceDays))
        {
            errors["CheckIn"] = [Messages.PublicMaxAdvanceDays(settings.MaxAdvanceDays)];
        }

        if (adults < 1 || adults > settings.MaxAdults)
        {
            errors["Adults"] = [Messages.PublicAdultsRange(settings.MaxAdults)];
        }

        if (children < 0 || children > settings.MaxChildren)
        {
            errors["Children"] = [Messages.PublicChildrenRange(settings.MaxChildren)];
        }

        if (errors.Count > 0)
        {
            throw PublicApiException.BadRequest(
                PublicErrorCodes.ValidationFailed,
                Messages.ValidationDefault,
                errors);
        }
    }

    /// <summary>
    /// Hold kuralları: aramanınkilerin tamamı + minimum gece + son dakika penceresi.
    /// Hold ucunda <c>MinNightsNotMet</c> bir "sonuç yok" hâli değil, bir <b>istek hatasıdır</b>:
    /// misafir zaten belirli bir teklifi tutmak istiyordur.
    /// </summary>
    public static void ValidateHold(
        Hotel hotel,
        DateOnly hotelToday,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int children)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        ValidateSearch(hotel, hotelToday, checkIn, checkOut, adults, children);

        var settings = hotel.PublicBookingSettings;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nights = checkOut.DayNumber - checkIn.DayNumber;

        if (nights < settings.MinNights)
        {
            errors["CheckOut"] = [Messages.PublicNightsRange(settings.MinNights, settings.MaxNights)];
        }

        if (settings.MinAdvanceHours > 0)
        {
            // Kapanış anı: giriş gününün check-in saati eksi MinAdvanceHours, otelin yerel
            // takviminde yorumlanır. Sunucunun saatine göre hesaplamak, saat dilimi farkı kadar
            // bir kayma üretirdi.
            var arrival = PublicTimeZone.ToInstant(checkIn, hotel.CheckInFromLocal, timeZone);
            if (now > arrival.AddHours(-settings.MinAdvanceHours))
            {
                errors["CheckIn"] = [Messages.PublicMinAdvanceHours(settings.MinAdvanceHours)];
            }
        }

        if (errors.Count > 0)
        {
            throw PublicApiException.BadRequest(
                PublicErrorCodes.ValidationFailed,
                Messages.ValidationDefault,
                errors);
        }
    }

    /// <summary>
    /// Kapasite kontrolü. Sözleşme bunu <b>409 <c>CAPACITY_EXCEEDED</c></b> olarak tanımlar (400
    /// değil): istek biçimsel olarak geçerlidir, seçilen oda tipiyle <i>çelişir</i>.
    /// </summary>
    public static void EnsureCapacity(int capacity, int adults, int children)
    {
        if (adults + children > capacity)
        {
            throw PublicApiException.Conflict(
                PublicErrorCodes.CapacityExceeded,
                Messages.PublicCapacityExceeded(capacity));
        }
    }
}
