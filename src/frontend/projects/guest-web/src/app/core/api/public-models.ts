/*
 * ===========================================================================
 * PUBLIC API — tip sozlesmesi
 * ===========================================================================
 *
 * Kaynak: docs/api-contracts-public-booking.md (public OpenAPI belgesinin
 * insan-okunur ozeti). Bu dosya elle yazildi cunku backend ucu bu turda henuz
 * canli degil; `ng-openapi-gen` ciktisi geldiginde bu dosya SILINIR ve
 * `@hotelcore/shared/public-api-types` ile degistirilir (mimari §11).
 *
 * KURALLAR (sozlesmeden birebir):
 *  - Public tarafta GUID YOKTUR: otel `hotelSlug`, oda tipi `roomTypeCode`,
 *    rezervasyon `bookingReference`, hold `holdToken`.
 *  - Para JSON'da nokta ondalikli `number`; bicimleme istemcinin isidir.
 *  - Konaklama gunleri `yyyy-MM-dd`; mutlak anlar ISO 8601 + otel yerel
 *    offset'i (UTC DEGIL — misafir kendi takvimiyle karsilastirabilmeli).
 *  - Alanlar `readonly`: yanit nesneleri istemcide degistirilmez. Ozellikle
 *    `orderSummary` degistirilirse sunucudaki hash ile uyusmaz.
 */

/** Gorsel — `width`/`height` API'den gelir; CLS'i onlemek icin ZORUNLUDUR. */
export interface PublicImage {
  readonly url: string;
  readonly alt: string;
  readonly width: number;
  readonly height: number;
  readonly sortOrder?: number;
}

/** Katalogdaki "ab" fiyati. `basis` PAngV acisindan etiketi belirler. */
export interface PublicFromPrice {
  readonly amount: number;
  readonly currency: string;
  readonly basis: 'BasePrice';
}

/** Kurtaxe kirilimi — `TaxProfile` ile ayni kural, yeniden hesaplanmaz. */
export interface PublicCityTax {
  readonly applies: boolean;
  readonly amount: number;
  readonly perPersonNight: number;
  readonly taxablePersons: number;
  readonly nights: number;
  readonly vatRate: number;
  readonly includedInTotal: boolean;
  readonly chargedOnlyIfStayTakesPlace: boolean;
  readonly childExemptionApplied: boolean;
  readonly childAgeLimit: number | null;
}

export interface PublicNightlyRate {
  readonly date: string;
  readonly gross: number;
}

/**
 * PAngV nesnesi — fiyat tasiyan TUM yanitlarda ayni sekil.
 * `totalGross` = konaklama + tum zorunlu kalemler (Kurtaxe DAHIL).
 */
export interface PublicPrice {
  readonly currency: string;
  readonly totalGross: number;
  readonly vatIncluded: boolean;
  readonly mandatoryExtrasIncluded: boolean;

  readonly accommodationGross: number;
  readonly accommodationNet: number;
  readonly accommodationVat: number;
  readonly accommodationVatRate: number;

  readonly cityTax: PublicCityTax;

  readonly nightly: readonly PublicNightlyRate[];
  readonly averageNightlyGross: number;

  readonly depositPercent: number;
  readonly amountDueAtProperty: number;
  readonly prepaidAmount: number;

  readonly optionalExtras: readonly unknown[];
}

export interface PublicCancellationPolicy {
  readonly type: 'Flexible' | 'Restricted';
  readonly freeCancellationUntil: string;
  readonly isFreeCancellationAvailable: boolean;
  readonly lateCancellationFeePercent: number;
  readonly lateCancellationFeeAmount: number;
  readonly noShowFeePercent: number;
  readonly noShowFeeAmount: number;
  readonly cityTaxRefundedOnCancellation: boolean;
  readonly policyTextKey: string;
}

export interface PublicPaymentOption {
  readonly method: 'PayAtProperty';
  readonly requiresGuarantee: boolean;
  readonly description?: string | null;
}

/** Otel kunyesi ve politikalari (`GET /hotels/{slug}`). */
export interface PublicHotel {
  readonly slug: string;
  readonly brandName: string;
  readonly name: string;
  readonly description: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly city: string;
  readonly country: string;
  readonly phone: string;
  readonly email: string;
  readonly currency: string;
  readonly timeZoneId: string;
  readonly defaultCulture: string;
  readonly supportedCultures: readonly string[];
  readonly checkInFromLocal: string;
  readonly checkOutUntilLocal: string;
  readonly images: readonly PublicImage[];
  readonly amenities: readonly string[];
  readonly booking: {
    readonly minNights: number;
    readonly maxNights: number;
    readonly maxAdvanceDays: number;
    readonly minAdvanceHours: number;
    readonly maxAdults: number;
    readonly maxChildren: number;
    readonly confirmationMode: 'Instant' | 'OnHotelAcceptance';
  };
  readonly cityTax: {
    readonly applies: boolean;
    readonly perPersonNight: number;
    readonly currency: string;
    readonly childrenExempt: boolean;
    readonly childAgeLimit: number | null;
    readonly chargedOnlyIfStayTakesPlace: boolean;
  };
  readonly cancellationPolicy: {
    readonly type: 'Flexible' | 'Restricted';
    readonly freeCancellationDaysBeforeArrival: number;
    readonly cutoffLocalTime: string;
    readonly lateCancellationFeePercent: number;
    readonly noShowFeePercent: number;
    readonly appliesToAccommodationOnly: boolean;
  };
  readonly paymentOptions: readonly PublicPaymentOption[];
}

/* ------------------------------------------------------------------------ */
/* Hukuki bilgiler (§5 DDG, DSGVO Art. 13)                                   */
/* ------------------------------------------------------------------------ */

export interface PublicImprint {
  readonly legalEntityName: string;
  readonly legalForm: string;
  readonly representedBy: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly city: string;
  readonly country: string;
  readonly phone: string;
  readonly email: string;
  readonly registerCourt: string | null;
  readonly registerNumber: string | null;
  readonly vatId: string | null;
  readonly supervisoryAuthority: string | null;
  readonly disputeResolution: {
    readonly participatesInAdr: boolean;
    readonly noticeKey: string;
    readonly odrPlatformUrl: string;
  };
}

export interface PublicLegalDocument {
  readonly key: 'terms' | 'privacy' | string;
  readonly title: string;
  readonly version: string;
  readonly culture: string;
  /** Sunucuda sanitize edilmis HTML (sozlesme §2.3). */
  readonly bodyHtml: string;
}

export interface PublicLegalResponse {
  readonly imprint: PublicImprint;
  readonly documents: readonly PublicLegalDocument[];
}

/* ------------------------------------------------------------------------ */
/* Katalog                                                                    */
/* ------------------------------------------------------------------------ */

export interface PublicRoomTypeSummary {
  readonly code: string;
  readonly name: string;
  readonly shortDescription: string;
  readonly capacity: number;
  readonly sizeSqm: number | null;
  readonly amenities: readonly string[];
  readonly image: PublicImage | null;
  readonly fromPrice: PublicFromPrice;
}

export interface PublicRoomTypeDetail {
  readonly code: string;
  readonly name: string;
  readonly shortDescription: string;
  readonly description: string;
  readonly capacity: number;
  readonly sizeSqm: number | null;
  readonly amenities: readonly string[];
  readonly images: readonly PublicImage[];
  readonly fromPrice: PublicFromPrice;
  readonly cancellationPolicy: PublicCancellationPolicy;
}

/* ------------------------------------------------------------------------ */
/* Musaitlik                                                                  */
/* ------------------------------------------------------------------------ */

export interface PublicAvailabilityQuery {
  readonly checkIn: string;
  readonly checkOut: string;
  readonly adults: number;
  readonly children: number;
}

export type PublicUnavailableReason =
  | 'NoRoomAvailable'
  | 'CapacityExceeded'
  | 'MinNightsNotMet';

export interface PublicOffer {
  readonly roomTypeCode: string;
  readonly name: string;
  readonly shortDescription: string;
  readonly capacity: number;
  readonly sizeSqm: number | null;
  readonly amenities: readonly string[];
  readonly image: PublicImage | null;
  readonly availability: {
    readonly isAvailable: boolean;
    /** 5'te kirpilir; kirpilmis deger GERCEK alt sinirdir (UWG §5). */
    readonly availableUnits: number;
    readonly availableUnitsCapped: boolean;
  };
  readonly price: PublicPrice;
  readonly cancellationPolicy: PublicCancellationPolicy;
}

export interface PublicUnavailableRoomType {
  readonly roomTypeCode: string;
  readonly name: string;
  readonly reason: PublicUnavailableReason;
}

export interface PublicAvailabilityResponse {
  readonly hotelSlug: string;
  readonly currency: string;
  readonly checkIn: string;
  readonly checkOut: string;
  readonly nights: number;
  readonly adults: number;
  readonly children: number;
  readonly offers: readonly PublicOffer[];
  readonly unavailableRoomTypes: readonly PublicUnavailableRoomType[];
}

/* ------------------------------------------------------------------------ */
/* Hold + §312j hukuki blok                                                   */
/* ------------------------------------------------------------------------ */

export interface PublicOrderSummaryComponent {
  readonly kind: 'Accommodation' | 'CityTax' | string;
  readonly labelKey: string;
  readonly label: string;
  readonly amount: number;
  readonly mandatory: boolean;
}

/**
 * §312j Abs. 2 BGB — dugmenin HEMEN USTUNDEKI zorunlu ozet.
 * Duz metin degil, alan alan gelir; boylece istemci bir kalemi "unutamaz".
 */
export interface PublicOrderSummary {
  readonly essentialFeatures: {
    readonly roomTypeName: string;
    readonly roomCount: number;
    readonly occupancy: { readonly adults: number; readonly children: number };
    readonly board: string;
  };
  readonly duration: {
    readonly checkIn: string;
    readonly checkOut: string;
    readonly nights: number;
    readonly checkInFromLocal: string;
    readonly checkOutUntilLocal: string;
    readonly timeZoneId: string;
  };
  readonly totalPrice: {
    readonly amount: number;
    readonly currency: string;
    readonly vatIncluded: boolean;
    readonly includesMandatoryCharges: boolean;
  };
  readonly components: readonly PublicOrderSummaryComponent[];
  /** `sha256:` + 64 hex. `POST /bookings` icinde AYNEN geri gonderilir. */
  readonly hash: string;
}

/** §312g Abs. 2 Nr. 9 BGB — tarihli konaklamada cayma hakki YOKTUR. */
export interface PublicWithdrawalRight {
  readonly applies: boolean;
  readonly legalBasis: string;
  readonly noticeKey: string;
  readonly noticeVersion: string;
}

/** §312j Abs. 3 BGB — Button-Losung. Etiket SUNUCUDAN gelir. */
export interface PublicOrderButton {
  readonly labelKey: string;
  readonly labelDe: string;
  readonly mustBeExactLabel: boolean;
}

export interface PublicLegalBlock {
  readonly withdrawalRight: PublicWithdrawalRight;
  readonly orderButton: PublicOrderButton;
  readonly terms: { readonly key: string; readonly version: string };
  readonly privacyNotice: { readonly key: string; readonly version: string };
  readonly contractConclusion: 'OnConfirmationEmail' | 'OnHotelAcceptance';
}

export type PublicGuestField =
  | 'firstName'
  | 'lastName'
  | 'email'
  | 'phone'
  | 'invoiceAddress'
  | 'estimatedArrivalLocalTime'
  | 'guestNote'
  | 'countryOfResidence';

export interface PublicCreateHoldRequest {
  readonly roomTypeCode: string;
  readonly checkIn: string;
  readonly checkOut: string;
  readonly adults: number;
  readonly children: number;
}

export interface PublicHold {
  readonly holdToken: string;
  readonly expiresAt: string;
  readonly expiresInSeconds: number;
  readonly hotelSlug: string;
  readonly roomTypeCode: string;
  readonly checkIn: string;
  readonly checkOut: string;
  readonly nights: number;
  readonly adults: number;
  readonly children: number;
  readonly price: PublicPrice;
  readonly cancellationPolicy: PublicCancellationPolicy;
  readonly orderSummary: PublicOrderSummary;
  readonly legal: PublicLegalBlock;
  readonly paymentOptions: readonly PublicPaymentOption[];
  readonly requiredGuestFields: readonly PublicGuestField[];
  readonly optionalGuestFields: readonly PublicGuestField[];
}

/* ------------------------------------------------------------------------ */
/* Rezervasyon                                                                */
/* ------------------------------------------------------------------------ */

export interface PublicInvoiceAddress {
  readonly company: string | null;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly city: string;
  readonly country: string;
  readonly vatId: string | null;
}

export interface PublicCreateBookingRequest {
  readonly holdToken: string;
  /** §312j kanit kaydi: gosterilen ozetin hash'i + gosterilen dugme metni. */
  readonly checkout: {
    readonly summaryHash: string;
    readonly orderButtonLabel: string;
  };
  readonly guest: {
    readonly firstName: string;
    readonly lastName: string;
    readonly email: string;
    readonly phone: string | null;
    readonly culture: string;
    readonly countryOfResidence: string | null;
  };
  readonly invoiceAddress: PublicInvoiceAddress | null;
  readonly stay: {
    readonly estimatedArrivalLocalTime: string | null;
    readonly guestNote: string | null;
  };
  readonly payment: { readonly method: string; readonly guarantee: null };
  readonly consents: {
    readonly termsAccepted: boolean;
    readonly termsVersion: string;
    readonly privacyNoticeAcknowledged: boolean;
    readonly privacyNoticeVersion: string;
    readonly withdrawalNoticeAcknowledged: boolean;
    readonly withdrawalNoticeVersion: string;
    readonly bookerIsAdult: boolean;
    readonly marketingOptIn: boolean;
  };
  readonly challengeToken: null;
}

export type PublicBookingStatus =
  | 'Confirmed'
  | 'InHouse'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow';

export interface PublicBookingResponse {
  readonly bookingReference: string;
  /** YALNIZCA 201 yanitinda doner; sonraki okumalarda yoktur. */
  readonly accessToken?: string;
  readonly accessTokenExpiresAt: string;
  readonly status: PublicBookingStatus;
  readonly createdAt: string;
  readonly hotel: {
    readonly slug: string;
    readonly name: string;
    readonly addressLine: string;
    readonly postalCode: string;
    readonly city: string;
    readonly country: string;
    readonly phone: string;
    readonly email: string;
    readonly timeZoneId: string;
  };
  readonly stay: {
    readonly roomTypeCode: string;
    readonly roomTypeName: string;
    readonly checkIn: string;
    readonly checkOut: string;
    readonly nights: number;
    readonly adults: number;
    readonly children: number;
    readonly checkInFromLocal: string;
    readonly checkOutUntilLocal: string;
    readonly estimatedArrivalLocalTime: string | null;
  };
  readonly guest: {
    readonly firstName: string;
    readonly lastName: string;
    readonly email: string;
    readonly phone: string | null;
  };
  readonly price: PublicPrice;
  readonly cancellation: PublicCancellationPolicy & {
    readonly canCancelOnline: boolean;
    readonly chargedFeeAmount: number | null;
  };
  readonly payment: {
    readonly method: string;
    readonly amountDueAtProperty: number;
    readonly prepaidAmount: number;
    readonly guarantee: null;
  };
  readonly legal: PublicLegalBlock;
  readonly confirmation: {
    readonly channel: 'Email';
    readonly recipientMasked: string;
    readonly sentAt: string | null;
    readonly documentVersion: string;
    readonly culture: string;
  };
}

export interface PublicCancelBookingRequest {
  readonly reason: string | null;
  /** Ucret dogacaksa ZORUNLU; ucretsizse `null`. */
  readonly acknowledgedFeeAmount: number | null;
}

export interface PublicBookingLookupRequest {
  readonly bookingReference: string;
  readonly email: string;
}
