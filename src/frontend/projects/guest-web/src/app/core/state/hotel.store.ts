import { Injectable, computed, inject } from '@angular/core';

import { PublicBookingApi } from '../api/public-booking.api';
import type { PublicHotel, PublicLegalResponse } from '../api/public-models';
import { toPublicError } from '../api/public-error';
import { asyncSlot } from './async-state';

/**
 * Otel kunyesi + hukuki belgeler (signal store).
 *
 * Bu iki uc **her sayfada** gerekir (alt bilgi, fiyat notlari, form sinirlari),
 * bu yuzden istek **tekillestirilir**: `load()` kac kez cagrilirsa cagrilsin
 * ag trafigi bir kezdir. Sunucu bu uclara `max-age=300` verir; istemci tarafi
 * tekilleştirme onun ustune, tek sayfa gezintisi icin gelir.
 */
@Injectable({ providedIn: 'root' })
export class HotelStore {
  private readonly api = inject(PublicBookingApi);

  private readonly hotelSlot = asyncSlot<PublicHotel>();
  private readonly legalSlot = asyncSlot<PublicLegalResponse>();

  readonly hotel = this.hotelSlot.data;
  readonly hotelState = this.hotelSlot.state;
  readonly legal = this.legalSlot.data;
  readonly legalState = this.legalSlot.state;

  /** Rezervasyon sinirlari — form bunlari kendi uydurmaz. */
  readonly limits = computed(() => {
    const booking = this.hotel()?.booking;
    return {
      minNights: booking?.minNights ?? 1,
      maxNights: booking?.maxNights ?? 30,
      maxAdults: booking?.maxAdults ?? 6,
      maxChildren: booking?.maxChildren ?? 6,
      maxAdvanceDays: booking?.maxAdvanceDays ?? 365,
    };
  });

  /** Kurtaxe bilgilendirmesi arama ekraninda da gorunur (PAngV). */
  readonly cityTax = computed(() => this.hotel()?.cityTax ?? null);

  load(): void {
    if (this.hotelSlot.state().status !== 'idle') {
      return;
    }
    this.hotelSlot.begin();
    this.api.getHotel().subscribe({
      next: (hotel) => this.hotelSlot.succeed(hotel),
      error: (error: unknown) => this.hotelSlot.fail(toPublicError(error)),
    });
  }

  loadLegal(): void {
    if (this.legalSlot.state().status !== 'idle') {
      return;
    }
    this.legalSlot.begin();
    this.api.getLegal().subscribe({
      next: (legal) => this.legalSlot.succeed(legal),
      error: (error: unknown) => this.legalSlot.fail(toPublicError(error)),
    });
  }

  /** Hata sonrasi "yeniden dene" — durumu sifirlayip tekrar ister. */
  retry(): void {
    this.hotelSlot.reset();
    this.load();
  }

  retryLegal(): void {
    this.legalSlot.reset();
    this.loadLegal();
  }
}
