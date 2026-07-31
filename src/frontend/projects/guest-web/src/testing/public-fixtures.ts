import type {
  PublicAvailabilityResponse,
  PublicBookingResponse,
  PublicCancellationPolicy,
  PublicHold,
  PublicOffer,
  PublicPrice,
} from '../app/core/api/public-models';

/**
 * Sozlesme belgesindeki (`docs/api-contracts-public-booking.md`) ORNEK
 * gövdelerden turetilmis test verisi.
 *
 * Sayilar bilincli olarak sozlesmedeki degismezleri saglar:
 *   accommodationNet + accommodationVat == accommodationGross
 *   totalGross == accommodationGross + cityTax.amount
 *   sum(nightly[].gross) == accommodationGross
 *   cityTax.amount == taxablePersons × nights × perPersonNight
 * Boylece "fiyat KDV + Kurtaxe dahil gosteriliyor mu" testi gercek bir veriyle
 * calisir, uydurma bir toplamla degil.
 */
export const PRICE: PublicPrice = {
  currency: 'EUR',
  totalGross: 468,
  vatIncluded: true,
  mandatoryExtrasIncluded: true,
  accommodationGross: 450,
  accommodationNet: 420.56,
  accommodationVat: 29.44,
  accommodationVatRate: 7,
  cityTax: {
    applies: true,
    amount: 18,
    perPersonNight: 3,
    taxablePersons: 2,
    nights: 3,
    vatRate: 0,
    includedInTotal: true,
    chargedOnlyIfStayTakesPlace: true,
    childExemptionApplied: true,
    childAgeLimit: 18,
  },
  nightly: [
    { date: '2026-08-10', gross: 150 },
    { date: '2026-08-11', gross: 150 },
    { date: '2026-08-12', gross: 150 },
  ],
  averageNightlyGross: 150,
  depositPercent: 0,
  amountDueAtProperty: 468,
  prepaidAmount: 0,
  optionalExtras: [],
};

export const CANCELLATION: PublicCancellationPolicy = {
  type: 'Flexible',
  freeCancellationUntil: '2026-08-07T18:00:00+02:00',
  isFreeCancellationAvailable: true,
  lateCancellationFeePercent: 90,
  lateCancellationFeeAmount: 405,
  noShowFeePercent: 90,
  noShowFeeAmount: 405,
  cityTaxRefundedOnCancellation: true,
  policyTextKey: 'legal.cancellation.flexible',
};

export const OFFER: PublicOffer = {
  roomTypeCode: 'DBL',
  name: 'Doppelzimmer',
  shortDescription: 'Ruhiger Hof, zwei Fenster.',
  capacity: 2,
  sizeSqm: 24,
  amenities: ['wifi', 'minibar'],
  image: null,
  availability: { isAvailable: true, availableUnits: 3, availableUnitsCapped: false },
  price: PRICE,
  cancellationPolicy: CANCELLATION,
};

export const AVAILABILITY: PublicAvailabilityResponse = {
  hotelSlug: 'berlin-mitte',
  currency: 'EUR',
  checkIn: '2026-08-10',
  checkOut: '2026-08-13',
  nights: 3,
  adults: 2,
  children: 0,
  offers: [OFFER],
  unavailableRoomTypes: [{ roomTypeCode: 'SUI', name: 'Suite', reason: 'NoRoomAvailable' }],
};

/** Sozlesme §5.1 ornegi — `orderSummary` ve `legal` bloklariyla birlikte. */
export function hold(overrides: Partial<PublicHold> = {}): PublicHold {
  return {
    holdToken: 'Vb3nQ8sT1kR6yPz0LmXhAw',
    expiresAt: '2026-07-31T09:15:00+02:00',
    expiresInSeconds: 900,
    hotelSlug: 'berlin-mitte',
    roomTypeCode: 'DBL',
    checkIn: '2026-08-10',
    checkOut: '2026-08-13',
    nights: 3,
    adults: 2,
    children: 0,
    price: PRICE,
    cancellationPolicy: CANCELLATION,
    orderSummary: {
      essentialFeatures: {
        roomTypeName: 'Doppelzimmer',
        roomCount: 1,
        occupancy: { adults: 2, children: 0 },
        board: 'None',
      },
      duration: {
        checkIn: '2026-08-10',
        checkOut: '2026-08-13',
        nights: 3,
        checkInFromLocal: '15:00',
        checkOutUntilLocal: '11:00',
        timeZoneId: 'Europe/Berlin',
      },
      totalPrice: {
        amount: 468,
        currency: 'EUR',
        vatIncluded: true,
        includesMandatoryCharges: true,
      },
      components: [
        {
          kind: 'Accommodation',
          labelKey: 'summary.accommodation',
          label: 'Uebernachtung 3 Naechte',
          amount: 450,
          mandatory: true,
        },
        {
          kind: 'CityTax',
          labelKey: 'summary.cityTax',
          label: 'Kurtaxe 2 Personen x 3 Naechte',
          amount: 18,
          mandatory: true,
        },
      ],
      hash: `sha256:${'9f2b'.repeat(16)}`,
    },
    legal: {
      withdrawalRight: {
        applies: false,
        legalBasis: 'BGB §312g Abs. 2 Nr. 9',
        noticeKey: 'legal.withdrawal.excluded.accommodation',
        noticeVersion: '2026-07-01',
      },
      orderButton: {
        labelKey: 'legal.orderButton.payable',
        labelDe: 'zahlungspflichtig buchen',
        mustBeExactLabel: true,
      },
      terms: { key: 'terms', version: '2026-07-01' },
      privacyNotice: { key: 'privacy', version: '2026-07-01' },
      contractConclusion: 'OnConfirmationEmail',
    },
    paymentOptions: [{ method: 'PayAtProperty', requiresGuarantee: false }],
    requiredGuestFields: ['firstName', 'lastName', 'email'],
    optionalGuestFields: ['phone', 'invoiceAddress', 'estimatedArrivalLocalTime', 'guestNote'],
    ...overrides,
  };
}

export function booking(overrides: Partial<PublicBookingResponse> = {}): PublicBookingResponse {
  const source = hold();
  return {
    bookingReference: 'K7QM-3XPD-9RTV',
    accessToken: 'hQ7pR2vK9mNc4XsA1TjW6bYdZ0f',
    accessTokenExpiresAt: '2026-09-12T00:00:00+02:00',
    status: 'Confirmed',
    createdAt: '2026-07-31T09:02:11+02:00',
    hotel: {
      slug: 'berlin-mitte',
      name: 'HotelCore Berlin Mitte',
      addressLine: 'Chausseestrasse 1',
      postalCode: '10115',
      city: 'Berlin',
      country: 'DE',
      phone: '+49 30 5550000',
      email: 'info@example.de',
      timeZoneId: 'Europe/Berlin',
    },
    stay: {
      roomTypeCode: 'DBL',
      roomTypeName: 'Doppelzimmer',
      checkIn: '2026-08-10',
      checkOut: '2026-08-13',
      nights: 3,
      adults: 2,
      children: 0,
      checkInFromLocal: '15:00',
      checkOutUntilLocal: '11:00',
      estimatedArrivalLocalTime: '18:00',
    },
    guest: {
      firstName: 'Jürgen',
      lastName: 'Müller',
      email: 'juergen.mueller@example.de',
      phone: null,
    },
    price: PRICE,
    cancellation: { ...CANCELLATION, canCancelOnline: true, chargedFeeAmount: null },
    payment: {
      method: 'PayAtProperty',
      amountDueAtProperty: 468,
      prepaidAmount: 0,
      guarantee: null,
    },
    legal: source.legal,
    confirmation: {
      channel: 'Email',
      recipientMasked: 'j***@e***.de',
      sentAt: null,
      documentVersion: '2026-07-01',
      culture: 'de',
    },
    ...overrides,
  };
}

/** Public uc adresleri — testler bunlari `HttpTestingController` ile yakalar. */
export const API = {
  hotel: '/api/v1/public/hotels/berlin-mitte',
  legal: '/api/v1/public/hotels/berlin-mitte/legal',
  roomTypes: '/api/v1/public/hotels/berlin-mitte/room-types',
  roomType: (code: string) => `/api/v1/public/hotels/berlin-mitte/room-types/${code}`,
  availability: '/api/v1/public/hotels/berlin-mitte/availability',
  holds: '/api/v1/public/hotels/berlin-mitte/holds',
  hold: (token: string) => `/api/v1/public/hotels/berlin-mitte/holds/${token}`,
  bookings: '/api/v1/public/hotels/berlin-mitte/bookings',
  booking: (token: string) => `/api/v1/public/hotels/berlin-mitte/bookings/${token}`,
  cancel: (token: string) => `/api/v1/public/hotels/berlin-mitte/bookings/${token}/cancel`,
  lookup: '/api/v1/public/hotels/berlin-mitte/bookings/lookup',
} as const;

/** `ProblemDetails` + `extensions.code` (sozlesme §1). */
export function problem(code: string, extras: Record<string, unknown> = {}) {
  return {
    type: 'https://httpstatuses.io/409',
    title: 'Conflict',
    status: 409,
    detail: 'Teknik aciklama — ekranda GORUNMEMELI.',
    traceId: '00-abc-123',
    extensions: { code },
    ...extras,
  };
}
