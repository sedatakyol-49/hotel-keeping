import { Injectable, computed, inject, signal } from '@angular/core';

import { PublicBookingApi } from '../api/public-booking.api';
import { toPublicError } from '../api/public-error';
import type { PublicAvailabilityQuery, PublicAvailabilityResponse } from '../api/public-models';
import { isIsoDate, nightsBetween } from '../dates/stay-dates';
import { transferredSlot } from './transferred-slot';

/** Istemci tarafi on dogrulama — sunucu kurallari yine sunucudadir. */
export type SearchQueryProblem =
  | 'invalidDates'
  | 'checkOutNotAfterCheckIn'
  | 'checkInInPast'
  | 'invalidOccupancy';

@Injectable({ providedIn: 'root' })
export class SearchStore {
  private readonly api = inject(PublicBookingApi);

  /*
   * Devredilen slot. Musaitlik ucu `Cache-Control: no-store` doner ve oyle
   * kalmali — ama bu, SUNUCUNUN AZ ONCE cizdigi sonucun tarayicida bir kez
   * daha cekilmesini gerektirmez. Devir olmadan sonuc listesi hidrasyondan
   * sonra kayboluyor ve yanit gelince geri geliyordu; 1440'ta olculen bedel
   * **CLS 0.35** (alt bilgi 307px asagi-yukari ziplyor).
   *
   * Kapsam SORGUNUN KENDISIDIR: baska bir tarih/kisi kombinasyonu icin
   * devralma olmaz, yani "eski aramanin fiyati yeni aramada gorunur" durumu
   * yapisal olarak imkansizdir. Kullanici formu degistirdiginde ya da
   * `retry()` cagrildiginda uc her zaman yeniden sorulur.
   */
  private readonly slot = transferredSlot<PublicAvailabilityResponse>('hc.availability');

  /** En son **calistirilan** sorgu (URL'den turer, formdan degil). */
  private readonly _query = signal<PublicAvailabilityQuery | null>(null);

  readonly query = this._query.asReadonly();
  readonly result = this.slot.data;
  readonly state = this.slot.state;
  readonly loading = this.slot.loading;
  readonly error = this.slot.error;

  readonly offers = computed(() => this.result()?.offers ?? []);
  readonly unavailable = computed(() => this.result()?.unavailableRoomTypes ?? []);

  /**
   * Musait tip yok ama sorgu basarili: bu bir **hata degildir** (sozlesme §4.1
   * acikca 200 doner). Ekran bunu ayri bir "bos sonuc" durumu olarak gosterir.
   */
  readonly emptyResult = computed(
    () => this.slot.ready() && this.offers().length === 0,
  );

  /**
   * Ayni sorgu iki kez calistirilmaz (SSR + hidrasyon sirasinda sayfa iki kez
   * kurulabilir; transfer cache olmasa bile ikinci istek atilmaz).
   */
  search(query: PublicAvailabilityQuery, force = false): void {
    const current = this._query();
    if (
      !force &&
      current !== null &&
      current.checkIn === query.checkIn &&
      current.checkOut === query.checkOut &&
      current.adults === query.adults &&
      current.children === query.children &&
      this.slot.state().status !== 'idle'
    ) {
      return;
    }

    this._query.set(query);

    /* Sunucu bu sorguyu az once cizdiyse tarayici sonucu devralir (bkz. slot). */
    const scope = scopeOf(query);
    if (!force && this.slot.adopt(scope)) {
      return;
    }

    this.slot.begin();
    this.api.getAvailability(query).subscribe({
      next: (response) => {
        this.slot.succeed(response);
        this.slot.handOver(response, scope);
      },
      error: (error: unknown) => this.slot.fail(toPublicError(error)),
    });
  }

  retry(): void {
    const query = this._query();
    if (query !== null) {
      this.search(query, true);
    }
  }

  /** Belirli bir oda tipinin teklifini bulur (detay sayfasi kullanir). */
  offerFor(roomTypeCode: string) {
    const code = roomTypeCode.toUpperCase();
    return this.offers().find((offer) => offer.roomTypeCode.toUpperCase() === code) ?? null;
  }
}

/** Devir kapsami: sorgunun tam kimligi. Tek bir alan bile degisirse eslesmez. */
function scopeOf(query: PublicAvailabilityQuery): string {
  return `${query.checkIn}|${query.checkOut}|${query.adults}|${query.children}`;
}

/**
 * Formdan gelen degerleri sorguya cevirir; gecersizse **sebep** dondurur.
 * Amac: kullaniciya "gecersiz tarih" demek yerine hangi kurali ihlal ettigini
 * soyleyebilmek.
 */
export function validateSearchQuery(
  query: PublicAvailabilityQuery,
  todayIsoDate: string,
): readonly SearchQueryProblem[] {
  const problems: SearchQueryProblem[] = [];

  if (!isIsoDate(query.checkIn) || !isIsoDate(query.checkOut)) {
    problems.push('invalidDates');
    return problems;
  }
  if (query.checkIn < todayIsoDate) {
    problems.push('checkInInPast');
  }
  if (nightsBetween(query.checkIn, query.checkOut) < 1) {
    problems.push('checkOutNotAfterCheckIn');
  }
  if (
    !Number.isInteger(query.adults) ||
    query.adults < 1 ||
    !Number.isInteger(query.children) ||
    query.children < 0
  ) {
    problems.push('invalidOccupancy');
  }
  return problems;
}
