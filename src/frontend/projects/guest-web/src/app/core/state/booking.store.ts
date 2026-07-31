import { Injectable, computed, inject, signal } from '@angular/core';

import { PublicBookingApi } from '../api/public-booking.api';
import { toPublicError, type PublicApiError } from '../api/public-error';
import type { PublicBookingResponse, PublicCreateBookingRequest } from '../api/public-models';
import { asyncSlot } from './async-state';

/**
 * Rezervasyon olusturma (signal store).
 *
 * VERI SAKLAMA KARARI: olusturulan rezervasyon **yalnizca bellekte** tutulur.
 * `accessToken` tasiyici bir kimlik bilgisidir (tek basina okuma + iptal
 * yetkisi verir); `localStorage`'a yazmak, paylasilan bir bilgisayarda sonraki
 * kullaniciya baskasinin rezervasyonunu acmak demektir. Sayfa yenilenirse
 * onay ekrani token'i **adresten** okur ve sunucudan yeniden ceker.
 *
 * §312j Abs. 2 ZORLAMASI: `SUMMARY_CHANGED` (409) bir hata mesaji degil, bir
 * **akis durdurucudur**. Ozet degistiyse gosterilen ozet artik dogru degildir;
 * rezervasyon tamamlanamaz, kullanici yeni ozeti gorup **yeniden onaylamalidir**.
 */
@Injectable({ providedIn: 'root' })
export class BookingStore {
  private readonly api = inject(PublicBookingApi);
  private readonly slot = asyncSlot<PublicBookingResponse>();

  /** Ozet/hukuki metin degisti: akis durdu, yeniden onay gerekiyor. */
  private readonly _requiresReconfirmation = signal(false);
  private readonly _submitting = signal(false);

  readonly booking = this.slot.data;
  readonly state = this.slot.state;
  readonly error = this.slot.error;
  readonly submitting = this._submitting.asReadonly();
  readonly requiresReconfirmation = this._requiresReconfirmation.asReadonly();

  /** Onay ekrani icin: 201 yanitindaki tek seferlik erisim token'i. */
  readonly accessToken = computed(() => this.booking()?.accessToken ?? null);

  submit(request: PublicCreateBookingRequest, onSuccess: (result: PublicBookingResponse) => void): void {
    if (this._submitting()) {
      return; // Cift tiklama korumasi — sunucu idempotent degil.
    }
    this._submitting.set(true);
    this._requiresReconfirmation.set(false);
    this.slot.begin();

    this.api.createBooking(request).subscribe({
      next: (booking) => {
        this._submitting.set(false);
        this.slot.succeed(booking);
        onSuccess(booking);
      },
      error: (error: unknown) => {
        const mapped: PublicApiError = toPublicError(error);
        this._submitting.set(false);
        if (mapped.code === 'SUMMARY_CHANGED' || mapped.code === 'LEGAL_TEXT_CHANGED') {
          this._requiresReconfirmation.set(true);
        }
        this.slot.fail(mapped);
      },
    });
  }

  /** Kullanici yeni ozeti onayladiktan sonra akis yeniden acilir. */
  acknowledgeReconfirmation(): void {
    this._requiresReconfirmation.set(false);
    this.slot.reset();
  }

  reset(): void {
    this._requiresReconfirmation.set(false);
    this._submitting.set(false);
    this.slot.reset();
  }
}
