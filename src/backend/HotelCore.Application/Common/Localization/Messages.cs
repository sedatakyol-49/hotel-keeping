using System.Globalization;
using System.Resources;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Common.Localization;

/// <summary>
/// İstemciye giden <b>tüm</b> hata ve doğrulama metinlerinin tek erişim noktası
/// (architecture.md §8). Metinler <c>Messages.resx</c> ailesinde tutulur:
/// <list type="bullet">
///   <item><c>Messages.resx</c> — nötr kaynak, dili <b>Almanca</b>'dır
///   (<c>SupportedCultures.Default == "de"</c> ile tutarlı; csproj'daki
///   <c>NeutralLanguage</c> sayesinde "de"/"de-DE" istekleri uydu assembly aranmadan
///   doğrudan buraya düşer),</item>
///   <item><c>Messages.en.resx</c>, <c>Messages.tr.resx</c> — uydu (satellite) kaynaklar.</item>
/// </list>
/// <para>
/// <b>Aktif dil</b> <see cref="RequestCulture.Current"/>'dan okunur; o da Api katmanındaki
/// <c>UseRequestLocalization</c>'ın ayarladığı <see cref="CultureInfo.CurrentUICulture"/>'ı
/// kullanır. Desteklenmeyen bir dil varsayılana (de) düşer. Böylece <c>Accept-Language</c>
/// başlığı burada <b>elle parse edilmez</b>.
/// </para>
/// <para>
/// <b>Parametreli mesajlar:</b> kaynak dizeleri yer tutuculu (<c>{0}</c>, <c>{1}</c> ...) tutulur
/// ve biçimlendirme yalnızca bu sınıftaki <c>Format</c> üzerinden yapılır. Tarihler her dilde
/// ISO 8601 (<c>yyyy-MM-dd</c>) olarak yazılır — API sözleşmesi tarihleri bu biçimde taşır;
/// para tutarları aktif kültüre göre biçimlenir (de: <c>10,00</c>, en: <c>10.00</c>).
/// Durum/enum adları ve alan adları (<c>checkIn</c>, <c>lineItems</c>, <c>Stornorechnung</c> ...)
/// teknik belirteçtir ve <b>çevrilmez</b>.
/// </para>
/// </summary>
public static class Messages
{
    /// <summary>Kaynak temel adı derleme sırasında üretilen manifest adıyla birebir aynıdır.</summary>
    private static readonly ResourceManager Resources = new(
        "HotelCore.Application.Common.Localization.Messages",
        typeof(Messages).Assembly);

    /// <summary>Tarihlerin her dilde aynı (ISO 8601) yazılmasını sağlayan biçim.</summary>
    private const string IsoDateFormat = "yyyy-MM-dd";

    // ---------------------------------------------------------------------------------------
    // ProblemDetails başlık/açıklama metinleri (Api/Middleware/ApiExceptionHandler)
    // ---------------------------------------------------------------------------------------

    public static string BadRequestTitle => Text("Problem_BadRequest_Title");

    public static string UnauthorizedTitle => Text("Problem_Unauthorized_Title");

    public static string ForbiddenTitle => Text("Problem_Forbidden_Title");

    public static string NotFoundTitle => Text("Problem_NotFound_Title");

    public static string ConflictTitle => Text("Problem_Conflict_Title");

    /// <summary>Hız sınırı aşımı (429) — public kanalda kullanılır.</summary>
    public static string TooManyRequestsTitle => Text("Problem_TooManyRequests_Title");

    public static string ClientClosedRequestTitle => Text("Problem_ClientClosedRequest_Title");

    public static string ClientClosedRequestDetail => Text("Problem_ClientClosedRequest_Detail");

    public static string UnhandledTitle => Text("Problem_Unhandled_Title");

    public static string UnhandledDetail => Text("Problem_Unhandled_Detail");

    public static string InvoicePdfNotImplementedTitle =>
        Text("Problem_InvoicePdfNotImplemented_Title");

    public static string InvoicePdfNotImplementedDetail(Guid invoiceId) =>
        Format("Problem_InvoicePdfNotImplemented_Detail", invoiceId);

    /// <summary>
    /// Model binding (System.Text.Json) katmanındaki enum hataları. Bu metinler
    /// <c>ApiExceptionHandler</c>'a uğramaz; MVC'nin ürettiği 400 yanıtında
    /// <c>errors</c> sözlüğüne yazılır.
    /// </summary>
    public static string EnumMustBeString(string allowedValues) =>
        Format("Validation_EnumMustBeString", allowedValues);

    public static string EnumInvalidValue(string? rawValue, string allowedValues) =>
        Format("Validation_EnumInvalidValue", rawValue, allowedValues);

    /// <summary>
    /// Framework'ün kendi ürettiği <c>ProblemDetails</c> yanıtlarında (401/403 gibi, istisna
    /// olmadan) başlığı isteğin diline çevirmek için kullanılan eşleme. Eşlenmeyen durum
    /// kodlarında <c>null</c> döner ve framework'ün başlığı korunur.
    /// </summary>
    public static string? TitleForStatusCode(int? statusCode) => statusCode switch
    {
        400 => BadRequestTitle,
        401 => UnauthorizedTitle,
        403 => ForbiddenTitle,
        404 => NotFoundTitle,
        409 => ConflictTitle,
        429 => TooManyRequestsTitle,
        500 => UnhandledTitle,
        _ => null
    };

    // ---------------------------------------------------------------------------------------
    // İstisna varsayılanları
    // ---------------------------------------------------------------------------------------

    public static string ForbiddenDefault => Text("Error_Forbidden_Default");

    public static string ValidationDefault => Text("Error_Validation_Default");

    public static string InvalidCredentials => Text("Auth_InvalidCredentials");

    /// <summary>
    /// <c>NotFoundException(entityName, key)</c> mesajı. <paramref name="entityName"/> teknik
    /// entity adıdır (<c>nameof(Room)</c>); varsa <c>Entity_*</c> anahtarıyla çevrilir, yoksa
    /// olduğu gibi gösterilir.
    /// </summary>
    public static string EntityNotFound(string entityName, object? key) =>
        Format("NotFound_Entity", EntityDisplayName(entityName), key);

    // ---------------------------------------------------------------------------------------
    // Kimlik / tenant bağlamı
    // ---------------------------------------------------------------------------------------

    public static string HotelHeaderRequired => Text("Validation_HotelHeaderRequired");

    public static string InvalidGuid => Text("Validation_InvalidGuid");

    public static string HotelAccessDenied(Guid hotelId) => Format("Forbidden_HotelAccess", hotelId);

    // ---------------------------------------------------------------------------------------
    // Auth
    // ---------------------------------------------------------------------------------------

    public static string NotAuthenticated => Text("Auth_NotAuthenticated");

    public static string UserInactiveOrMissing => Text("Auth_UserInactiveOrMissing");

    public static string InvalidRefreshToken => Text("Auth_InvalidRefreshToken");

    // ---------------------------------------------------------------------------------------
    // Müsaitlik
    // ---------------------------------------------------------------------------------------

    public static string RoomOutOfOrder(string roomNumber) =>
        Format("Conflict_RoomOutOfOrder", roomNumber);

    public static string RoomNotAvailable(
        string roomNumber,
        DateOnly from,
        DateOnly to,
        string reservationNumber,
        DateOnly conflictFrom,
        DateOnly conflictTo) =>
        Format(
            "Conflict_RoomNotAvailable",
            roomNumber,
            Iso(from),
            Iso(to),
            reservationNumber,
            Iso(conflictFrom),
            Iso(conflictTo));

    public static string ToAfterFromNight => Text("Validation_ToAfterFrom_Night");

    public static string ToAfterFromDay => Text("Validation_ToAfterFrom_Day");

    public static string AvailabilityRangeTooLong(int maxDays) =>
        Format("Validation_AvailabilityRangeTooLong", maxDays);

    public static string OccupancyRangeTooLong(int maxDays) =>
        Format("Validation_OccupancyRangeTooLong", maxDays);

    // ---------------------------------------------------------------------------------------
    // Departmanlar / personel
    // ---------------------------------------------------------------------------------------

    public static string DepartmentNotFound => Text("NotFound_Department");

    public static string DepartmentNameTaken(string name) =>
        Format("Conflict_DepartmentNameTaken", name);

    public static string DepartmentHasEmployees => Text("Conflict_DepartmentHasEmployees");

    public static string EmployeeNotFound => Text("NotFound_Employee");

    public static string StaffNumberTaken(string staffNumber) =>
        Format("Conflict_StaffNumberTaken", staffNumber);

    // ---------------------------------------------------------------------------------------
    // Misafirler ve dil seçimi
    // ---------------------------------------------------------------------------------------

    public static string GuestNotFound => Text("NotFound_Guest");

    public static string GuestHasActiveReservations => Text("Conflict_GuestHasActiveReservations");

    /// <summary>Desteklenen dillerin listesi (kültürden bağımsız kodlar: <c>de, en, tr</c>).</summary>
    public static string SupportedCultureList =>
        Format("Validation_SupportedCultures", string.Join(", ", SupportedCultures.All));

    /// <summary>Aynı liste; çeviri sözlüğü anahtarları için kullanılan varyant.</summary>
    public static string SupportedCultureCodeList =>
        Format("Validation_SupportedCultureCodes", string.Join(", ", SupportedCultures.All));

    // ---------------------------------------------------------------------------------------
    // Head Office / otel ayarları
    // ---------------------------------------------------------------------------------------

    public static string HeadOfficeNotFound => Text("NotFound_HeadOffice");

    public static string NoHeadOfficeInIdentity => Text("Forbidden_NoHeadOffice");

    public static string HotelNotFound => Text("NotFound_Hotel");

    public static string CurrencyFormat => Text("Validation_CurrencyFormat");

    public static string ChildAgeLimit(int maxAge) => Format("Validation_ChildAgeLimit", maxAge);

    // Misafire açık kanal ayarları (admin tarafı) — yeni izin anahtarı YOKTUR, alanlar mevcut
    // Settings.Manage altında yönetilir.

    public static string InvalidTimeZone => Text("Validation_InvalidTimeZone");

    public static string InvalidConfirmationMode => Text("Validation_InvalidConfirmationMode");

    public static string InvalidCancellationPolicyType => Text("Validation_InvalidCancellationPolicyType");

    public static string InvalidPublicSlug => Text("Validation_InvalidPublicSlug");

    public static string PublicSlugRequired => Text("Validation_PublicSlugRequired");

    public static string LegalEntityNameRequired => Text("Validation_LegalEntityNameRequired");

    /// <summary>Slug canlı satırlar arasında <b>global</b> benzersizdir; çakışma 409 üretir.</summary>
    public static string PublicSlugTaken(string slug) => Format("Conflict_PublicSlugTaken", slug);

    // ---------------------------------------------------------------------------------------
    // Faturalar (GoBD)
    // ---------------------------------------------------------------------------------------

    public static string ReservationNotFound => Text("NotFound_Reservation");

    public static string PaymentDateInFuture => Text("Validation_PaymentDateInFuture");

    public static string PaymentExceedsOutstanding(decimal outstanding, string currency, decimal amount) =>
        Format("Conflict_PaymentExceedsOutstanding", outstanding, currency, amount);

    public static string PaymentOnDraftInvoice => Text("Conflict_PaymentOnDraft");

    public static string PaymentOnCancelledInvoice => Text("Conflict_PaymentOnCancelled");

    public static string InvoiceAlreadyPaid => Text("Conflict_InvoiceAlreadyPaid");

    public static string PaymentNotAllowedInStatus(InvoiceStatus status) =>
        Format("Conflict_PaymentNotAllowedInStatus", status);

    public static string PaymentOnNonPositiveInvoice => Text("Conflict_PaymentOnNonPositiveInvoice");

    public static string InvoiceAlreadyCancelled => Text("Conflict_InvoiceAlreadyCancelled");

    public static string ReservationAlreadyInvoiced => Text("Conflict_ReservationAlreadyInvoiced");

    public static string InvoiceNumberSequenceRace => Text("Conflict_InvoiceNumberSequenceRace");

    public static string InvoiceNotDraft(InvoiceStatus status) =>
        Format("Conflict_InvoiceNotDraft", status);

    public static string InvoiceNotDraftForFinalize(InvoiceStatus status) =>
        Format("Conflict_InvoiceNotDraftForFinalize", status);

    public static string InvoiceWithoutLines => Text("Conflict_InvoiceWithoutLines");

    public static string InvoiceGuestFromReservation => Text("Conflict_InvoiceGuestFromReservation");

    public static string InvoiceMaxLineItems(int maxLineItems) =>
        Format("Validation_InvoiceMaxLineItems", maxLineItems);

    public static string InvoiceGuestRequired => Text("Validation_InvoiceGuestRequired");

    public static string InvoiceLinesNotAllowed => Text("Validation_InvoiceLinesNotAllowed");

    /// <summary>
    /// Rezervasyondan üretilen faturada <c>PUT</c> gövdesi yalnızca <c>Extra</c> satır taşıyabilir
    /// (oda ücreti ve Kurtaxe sunucunundur).
    /// </summary>
    public static string InvoiceReservationLinesServerOwned =>
        Text("Validation_InvoiceReservationLinesServerOwned");

    public static string InvoiceNeedsReservationOrLines =>
        Text("Validation_InvoiceNeedsReservationOrLines");

    public static string InvoiceNeedsLines => Text("Validation_InvoiceNeedsLines");

    public static string ToNotBeforeFrom => Text("Validation_ToNotBeforeFrom");

    // ---------------------------------------------------------------------------------------
    // Fiyat planları
    // ---------------------------------------------------------------------------------------

    public static string RatePlanOverlap(
        ReservationChannel? channel,
        string planName,
        DateOnly validFrom,
        DateOnly validTo) =>
        Format(
            "Conflict_RatePlanOverlap",
            channel?.ToString() ?? Text("RatePlan_AllChannels"),
            planName,
            Iso(validFrom),
            Iso(validTo));

    public static string RatePlanInUse => Text("Conflict_RatePlanInUse");

    public static string ValidToNotBeforeValidFrom => Text("Validation_ValidToNotBeforeValidFrom");

    // ---------------------------------------------------------------------------------------
    // Raporlar
    // ---------------------------------------------------------------------------------------

    public static string ReportToNotBeforeFrom => Text("Validation_ReportToNotBeforeFrom");

    public static string ReportRangeTooLong(int maxDays) =>
        Format("Validation_ReportRangeTooLong", maxDays);

    // ---------------------------------------------------------------------------------------
    // Rezervasyonlar
    // ---------------------------------------------------------------------------------------

    public static string CheckInBeforeArrival(DateOnly checkIn, DateOnly today) =>
        Format("Conflict_CheckInBeforeArrival", Iso(checkIn), Iso(today));

    public static string CheckInRoomOutOfOrder(string roomNumber) =>
        Format("Conflict_CheckInRoomOutOfOrder", roomNumber);

    public static string StayNightsRange(int maxNights) =>
        Format("Validation_StayNightsRange", maxNights);

    public static string RoomCapacityExceeded(string roomNumber, int capacity, int guests) =>
        Format("Validation_RoomCapacityExceeded", roomNumber, capacity, guests);

    public static string ReservationSameStatus(ReservationStatus status) =>
        Format("Conflict_ReservationSameStatus", status);

    public static string ReservationInvalidTransition(
        ReservationStatus from,
        ReservationStatus to,
        IReadOnlyCollection<ReservationStatus> allowed)
    {
        ArgumentNullException.ThrowIfNull(allowed);

        var allowedText = allowed.Count == 0
            ? Text("Reservation_NoAllowedTransitions")
            : string.Join(", ", allowed);

        return Format("Conflict_ReservationInvalidTransition", from, to, allowedText);
    }

    public static string ReservationNotModifiable(ReservationStatus status) =>
        Format("Conflict_ReservationNotModifiable", status);

    public static string CheckOutAfterCheckIn => Text("Validation_CheckOutAfterCheckIn");

    public static string MaxNights(int maxNights) => Format("Validation_MaxNights", maxNights);

    public static string InitialReservationStatus => Text("Validation_InitialReservationStatus");

    public static string ToAfterFrom => Text("Validation_ToAfterFrom");

    // ---------------------------------------------------------------------------------------
    // Oda tipleri
    // ---------------------------------------------------------------------------------------

    public static string RoomTypeCodeTaken(string code) => Format("Conflict_RoomTypeCodeTaken", code);

    public static string RoomTypeHasRooms => Text("Conflict_RoomTypeHasRooms");

    public static string AmenitiesMaxCount(int maxCount) =>
        Format("Validation_AmenitiesMaxCount", maxCount);

    public static string AmenityMaxLength(int maxLength) =>
        Format("Validation_AmenityMaxLength", maxLength);

    public static string AmenitiesMaxTotalLength(int maxLength) =>
        Format("Validation_AmenitiesMaxTotalLength", maxLength);

    public static string TranslationNameMaxLength(int maxLength) =>
        Format("Validation_TranslationNameMaxLength", maxLength);

    public static string TranslationDescriptionMaxLength(int maxLength) =>
        Format("Validation_TranslationDescriptionMaxLength", maxLength);

    // ---------------------------------------------------------------------------------------
    // Odalar
    // ---------------------------------------------------------------------------------------

    public static string RoomNumberTaken(string number) => Format("Conflict_RoomNumberTaken", number);

    public static string RoomHasFutureReservations => Text("Conflict_RoomHasFutureReservations");

    /// <summary>
    /// Oda silme reddi: odanın yürürlükteki (iptal edilmemiş) ve <b>henüz faturalanmamış</b> bir
    /// rezervasyonu var. Hangi rezervasyon olduğu mesajda açıkça yazar — aksi hâlde kullanıcı
    /// engeli kaldıracak eylemi bulamaz. Dayanak: GoBD / AO §147 (kayıtların 10 yıl erişilebilir
    /// ve makineyle değerlendirilebilir kalması).
    /// </summary>
    public static string RoomHasUnbilledReservation(
        string reservationNumber,
        DateOnly checkIn,
        DateOnly checkOut) =>
        Format("Conflict_RoomHasUnbilledReservation", reservationNumber, Iso(checkIn), Iso(checkOut));

    // ---------------------------------------------------------------------------------------
    // Vardiyalar
    // ---------------------------------------------------------------------------------------

    public static string ShiftAlreadyExists(DateOnly date) =>
        Format("Conflict_ShiftAlreadyExists", Iso(date));

    public static string IsoWeekFormat => Text("Validation_IsoWeekFormat");

    public static string ToRequiredWithFrom => Text("Validation_ToRequiredWithFrom");

    public static string FromRequiredWithTo => Text("Validation_FromRequiredWithTo");

    public static string ShiftRangeTooLong(int maxDays) =>
        Format("Validation_ShiftRangeTooLong", maxDays);

    // ---------------------------------------------------------------------------------------
    // Mesai kayıtları (Zeiterfassung)
    // ---------------------------------------------------------------------------------------

    public static string NoOpenTimeEntry => Text("Conflict_NoOpenTimeEntry");

    public static string OpenTimeEntryExists => Text("Conflict_OpenTimeEntryExists");

    public static string BreakMinutesRange(int maxMinutes) =>
        Format("Validation_BreakMinutesRange", maxMinutes);

    public static string ClockOutAfterClockIn => Text("Validation_ClockOutAfterClockIn");

    public static string BreakExceedsWorkedTime(int workedMinutes) =>
        Format("Validation_BreakExceedsWorkedTime", workedMinutes);

    public static string ClockInNotInFuture => Text("Validation_ClockInNotInFuture");

    public static string ClockOutNotInFuture => Text("Validation_ClockOutNotInFuture");

    // ---------------------------------------------------------------------------------------
    // İzinler
    // ---------------------------------------------------------------------------------------

    public static string VacationNotCancellable(VacationStatus status) =>
        Format("Conflict_VacationNotCancellable", status);

    public static string VacationCancelForbidden => Text("Forbidden_VacationCancel");

    public static string VacationCancelOwnOnly => Text("Forbidden_VacationCancelOwnOnly");

    public static string VacationAlreadyDecided(VacationStatus status) =>
        Format("Conflict_VacationAlreadyDecided", status);

    public static string VacationOverlap(DateOnly from, DateOnly to) =>
        Format("Conflict_VacationOverlap", $"{Iso(from)} - {Iso(to)}");

    public static string VacationToNotBeforeFrom => Text("Validation_VacationToNotBeforeFrom");

    public static string VacationMaxDays(int maxDays) =>
        Format("Validation_VacationMaxDays", maxDays);

    // ---------------------------------------------------------------------------------------
    // Misafire açık (public) rezervasyon kanalı
    // ---------------------------------------------------------------------------------------
    // Not: bu metinler istemci MANTIĞINDA kullanılmaz. Public yanıtlar dilden bağımsız bir
    // `extensions.code` anahtarı taşır (api-contracts-public-booking.md §1); metin yalnızca
    // kullanıcıya gösterilir ve serbestçe yeniden yazılabilir.

    public static string PublicHotelNotFound => Text("Public_HotelNotFound");

    public static string PublicBrandNotFound => Text("Public_BrandNotFound");

    public static string PublicRoomTypeNotFound => Text("Public_RoomTypeNotFound");

    public static string PublicHoldNotFound => Text("Public_HoldNotFound");

    public static string PublicBookingNotFound => Text("Public_BookingNotFound");

    public static string PublicHoldExpired => Text("Public_HoldExpired");

    public static string PublicHoldAlreadyUsed => Text("Public_HoldAlreadyUsed");

    public static string PublicRoomNoLongerAvailable => Text("Public_RoomNoLongerAvailable");

    public static string PublicCapacityExceeded(int capacity) =>
        Format("Public_CapacityExceeded", capacity);

    public static string PublicSummaryChanged => Text("Public_SummaryChanged");

    public static string PublicLegalTextChanged => Text("Public_LegalTextChanged");

    public static string PublicCancellationNotAllowed => Text("Public_CancellationNotAllowed");

    public static string PublicFeeAcknowledgementRequired(decimal amount, string currency) =>
        Format("Public_FeeAcknowledgementRequired", amount, currency);

    public static string PublicBookingAlreadyCancelled => Text("Public_BookingAlreadyCancelled");

    public static string PublicRateLimitExceeded => Text("Public_RateLimitExceeded");

    public static string PublicCardDataNotAccepted => Text("Public_CardDataNotAccepted");

    public static string PublicChannelNotConfigured => Text("Public_ChannelNotConfigured");

    public static string PublicBotChallengeFailed => Text("Public_BotChallengeFailed");

    public static string PublicCheckInInPast(DateOnly hotelToday) =>
        Format("Validation_PublicCheckInInPast", Iso(hotelToday));

    public static string PublicNightsRange(int minNights, int maxNights) =>
        Format("Validation_PublicNightsRange", minNights, maxNights);

    public static string PublicMaxAdvanceDays(int maxAdvanceDays) =>
        Format("Validation_PublicMaxAdvanceDays", maxAdvanceDays);

    public static string PublicMinAdvanceHours(int minAdvanceHours) =>
        Format("Validation_PublicMinAdvanceHours", minAdvanceHours);

    public static string PublicAdultsRange(int maxAdults) =>
        Format("Validation_PublicAdultsRange", maxAdults);

    public static string PublicChildrenRange(int maxChildren) =>
        Format("Validation_PublicChildrenRange", maxChildren);

    public static string PublicConsentRequired => Text("Validation_PublicConsentRequired");

    public static string PublicHoldTokenFormat => Text("Validation_PublicHoldTokenFormat");

    public static string PublicSummaryHashFormat => Text("Validation_PublicSummaryHashFormat");

    public static string PublicFeeNotExpected => Text("Validation_PublicFeeNotExpected");

    public static string PublicArrivalTimeFormat => Text("Validation_PublicArrivalTimeFormat");

    public static string PublicCountryUnknown => Text("Validation_PublicCountryUnknown");

    public static string PublicPaymentMethodNotOffered => Text("Validation_PublicPaymentMethod");

    // ---------------------------------------------------------------------------------------
    // Kalıcılık katmanı (AppDbContext): veritabanı kısıtları ve GoBD guard'ı
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Benzersizlik ihlali (PostgreSQL SQLSTATE <c>23505</c>) çevirisi. Hangi kısıtın/tablonun
    /// ihlal edildiği <b>kasıtlı olarak parametre değildir</b>: şema detayı istemciye sızmaz,
    /// teşhis için yalnızca sunucu log'una yazılır.
    /// </summary>
    public static string UniqueViolation => Text("Conflict_UniqueViolation");

    /// <summary>
    /// Dışlama (EXCLUDE) kısıtı ihlali (SQLSTATE <c>23P01</c>) çevirisi — aynı şekilde kısıt adı
    /// içermez.
    /// </summary>
    public static string ExclusionViolation => Text("Conflict_ExclusionViolation");

    /// <summary>GoBD 10 yıl saklama zorunluluğu: fatura hiçbir yoldan silinemez.</summary>
    public static string InvoiceNotDeletable(Guid invoiceId) =>
        Format("Conflict_InvoiceNotDeletable", invoiceId);

    /// <summary>
    /// GoBD değiştirilemezlik guard'ı — fatura içeriği. <paramref name="changedProperties"/>
    /// teknik alan adlarının listesidir (<c>GrossAmount</c> ...) ve çevrilmez.
    /// </summary>
    public static string InvoiceImmutable(
        Guid invoiceId,
        InvoiceStatus status,
        string changedProperties) =>
        Format("Conflict_InvoiceImmutable", invoiceId, status, changedProperties);

    /// <summary>
    /// GoBD değiştirilemezlik guard'ı — fatura satırları. <paramref name="operation"/> EF Core
    /// girdi durumunun adıdır (<c>Added</c>/<c>Modified</c>/<c>Deleted</c>) ve çevrilmez.
    /// </summary>
    public static string InvoiceLineItemsImmutable(
        Guid invoiceId,
        InvoiceStatus status,
        Guid lineItemId,
        string operation) =>
        Format("Conflict_InvoiceLineItemsImmutable", invoiceId, status, lineItemId, operation);

    // ---------------------------------------------------------------------------------------
    // Altyapı
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Arama ve biçimlendirme için kullanılacak kültür. <see cref="RequestCulture.Current"/>
    /// desteklenmeyen dilleri varsayılana indirger, bu yüzden burada ek kontrol gerekmez.
    /// </summary>
    private static CultureInfo Culture => CultureInfo.GetCultureInfo(RequestCulture.Current);

    /// <summary>
    /// Ham kaynak metni. Anahtar bulunamazsa (yalnızca geliştirme hatası) anahtarın kendisi
    /// döner: istek 500 ile düşmez, eksiklik yanıtta görünür olur.
    /// </summary>
    private static string Text(string key) => Resources.GetString(key, Culture) ?? key;

    /// <summary>Parametreli mesajların <b>tek</b> biçimlendirme noktası.</summary>
    private static string Format(string key, params object?[] arguments) =>
        string.Format(Culture, Text(key), arguments);

    /// <summary>Tarihler her dilde ISO 8601 yazılır (API sözleşmesiyle aynı biçim).</summary>
    private static string Iso(DateOnly date) => date.ToString(IsoDateFormat, CultureInfo.InvariantCulture);

    /// <summary>Teknik entity adının çevirisi; karşılığı yoksa ad olduğu gibi kullanılır.</summary>
    private static string EntityDisplayName(string entityName) =>
        Resources.GetString("Entity_" + entityName, Culture) ?? entityName;
}
