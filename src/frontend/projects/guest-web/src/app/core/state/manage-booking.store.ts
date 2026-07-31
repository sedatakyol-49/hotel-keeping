import { Injectable, computed, inject, signal } from '@angular/core';

import { PublicBookingApi } from '../api/public-booking.api';
import { toPublicError, type PublicApiError } from '../api/public-error';
import type { PublicBookingResponse } from '../api/public-models';
import { asyncSlot } from './async-state';

/**
 * Rezervasyon sorgulama, iptal ve baglanti yenileme (signal store).
 *
 * IPTAL UCRETI SOZLESMESI (§7.3): ucretsiz pencere kapandiysa iptal **yine
 * mumkundur**, ama sunucu tutar teyidi ister (`acknowledgedFeeAmount`).
 * Amac, misafirin ucreti gormeden iptal etmesini engellemektir; bu yuzden
 * arayuz tutar teyidini **ayri bir adim** yapar. Sunucu tutari farkli
 * hesaplarsa (409 `FEE_ACKNOWLEDGEMENT_REQUIRED`) dogru tutar `errors`
 * icinden okunur ve kullaniciya yeniden gosterilir.
 */
@Injectable({ providedIn: 'root' })
export class ManageBookingStore {
  private readonly api = inject(PublicBookingApi);
  private readonly slot = asyncSlot<PublicBookingResponse>();

  private readonly _cancelling = signal(false);
  private readonly _cancelError = signal<PublicApiError | null>(null);
  private readonly _lookupSent = signal(false);
  private readonly _lookupPending = signal(false);
  private readonly _lookupError = signal<PublicApiError | null>(null);

  readonly booking = this.slot.data;
  readonly state = this.slot.state;
  readonly loading = this.slot.loading;
  readonly error = this.slot.error;
  readonly cancelling = this._cancelling.asReadonly();
  readonly cancelError = this._cancelError.asReadonly();
  readonly lookupSent = this._lookupSent.asReadonly();
  readonly lookupPending = this._lookupPending.asReadonly();
  readonly lookupError = this._lookupError.asReadonly();

  /** Online iptal mumkun mu? (`InHouse`/`Completed` icin sunucu 409 doner.) */
  readonly canCancel = computed(() => {
    const booking = this.booking();
    return (
      booking !== null && booking.cancellation.canCancelOnline && booking.status === 'Confirmed'
    );
  });

  /** Simdi iptal edilirse dogacak ucret (0 ise ucretsiz pencere icindeyiz). */
  readonly cancellationFee = computed(() => {
    const booking = this.booking();
    if (booking === null) {
      return 0;
    }
    return booking.cancellation.isFreeCancellationAvailable
      ? 0
      : booking.cancellation.lateCancellationFeeAmount;
  });

  /**
   * Sunucunun bildirdigi dogru tutar (409 sonrasi). `errors` icindeki
   * mesajlardan sayi ayiklamak yerine, kullaniciya guncellenen rezervasyonu
   * yeniden yuklemesini sunuyoruz; boylece tutar tek kaynaktan gelir.
   */
  readonly feeMismatch = computed(
    () => this._cancelError()?.code === 'FEE_ACKNOWLEDGEMENT_REQUIRED',
  );

  load(accessToken: string): void {
    this.slot.begin();
    this._cancelError.set(null);
    this.api.getBooking(accessToken).subscribe({
      next: (booking) => this.slot.succeed(booking),
      error: (error: unknown) => this.slot.fail(toPublicError(error)),
    });
  }

  /** Onay ekranindan gelen taze rezervasyon: tekrar istek atmaya gerek yok. */
  adopt(booking: PublicBookingResponse): void {
    this.slot.succeed(booking);
  }

  cancel(accessToken: string, reason: string | null, acknowledgedFeeAmount: number | null): void {
    if (this._cancelling()) {
      return;
    }
    this._cancelling.set(true);
    this._cancelError.set(null);

    this.api.cancelBooking(accessToken, { reason, acknowledgedFeeAmount }).subscribe({
      next: (booking) => {
        this._cancelling.set(false);
        this.slot.succeed(booking);
      },
      error: (error: unknown) => {
        const mapped = toPublicError(error);
        this._cancelling.set(false);
        this._cancelError.set(mapped);
        /* Tutar uyusmazliginda dogru degeri sunucudan tazele. */
        if (mapped.code === 'FEE_ACKNOWLEDGEMENT_REQUIRED') {
          this.api.getBooking(accessToken).subscribe({
            next: (booking) => this.slot.succeed(booking),
            error: () => undefined,
          });
        }
      },
    });
  }

  /**
   * Baglantiyi kaybeden misafir: uc **hicbir kosulda veri dondurmez** ve her
   * durumda 202 doner. Bu yuzden arayuz de "gonderildi" demez, **"eslesme
   * varsa gonderildi"** der; aksi halde bir rezervasyonun varligi sizardi.
   */
  lookup(bookingReference: string, email: string): void {
    if (this._lookupPending()) {
      return;
    }
    this._lookupPending.set(true);
    this._lookupError.set(null);

    this.api.lookupBooking({ bookingReference, email }).subscribe({
      next: () => {
        this._lookupPending.set(false);
        this._lookupSent.set(true);
      },
      error: (error: unknown) => {
        this._lookupPending.set(false);
        this._lookupError.set(toPublicError(error));
      },
    });
  }

  resetLookup(): void {
    this._lookupSent.set(false);
    this._lookupError.set(null);
  }
}
