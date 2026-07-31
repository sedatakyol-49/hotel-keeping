import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

import { PublicBookingApi } from '../api/public-booking.api';
import { toPublicError, type PublicApiError } from '../api/public-error';
import type { PublicCreateHoldRequest, PublicHold } from '../api/public-models';
import { BrowserStorage } from '../storage/browser-storage';
import { asyncSlot } from './async-state';

/**
 * ===========================================================================
 * HOLD (15 dakikalik gecici tutma) — durum ve geri sayim
 * ===========================================================================
 *
 * Hold'un ne ise yaradigi mimari §5.2'de: §312j Abs. 2 ozetinin **gercekten
 * odenecek** tutari gostermesi, misafir butona bastiktan sonra "son oda satildi"
 * dememek ve somut odayi dondurmak.
 *
 * ISTEMCININ SORUMLULUKLARI (bu store):
 *  1) Kalan sureyi **gostermek** — misafir aceleyi bilmeden aceleye getirilemez.
 *  2) Sure dolunca ne olacagini **onceden** soylemek (metin bileşende).
 *  3) Dolduktan sonra bir **kurtarma yolu** sunmak: ayni parametrelerle yeni
 *     hold. Sunucu uzatma vermez (bilincli); yeni teklif yeni fiyat demek
 *     olabilir ve bu kullaniciya acikca gosterilir.
 *
 * GERI SAYIM DRIFT'SIZDIR: her tik `expiresAt`'ten yeniden hesaplanir, sayaci
 * bir azaltmakla degil. Sekme uyutulup geri donuldugunde sure dogru kalir —
 * "ekranda 8 dakika yaziyordu ama hold dusmustu" durumu olusmaz.
 */

/** Depoda yalnizca token durur; teklif her zaman sunucudan **yeniden** okunur. */
const HOLD_STORAGE_KEY = 'hc.hold';

@Injectable({ providedIn: 'root' })
export class HoldStore {
  private readonly api = inject(PublicBookingApi);
  private readonly storage = inject(BrowserStorage);
  private readonly slot = asyncSlot<PublicHold>();

  private readonly _now = signal(Date.now());
  private readonly _expired = signal(false);
  /** Yenileme oncesi fiyat — yeni teklif farkliysa acikca gosterilir. */
  private readonly _previousTotal = signal<number | null>(null);
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly hold = this.slot.data;
  readonly state = this.slot.state;
  readonly loading = this.slot.loading;
  readonly error = this.slot.error;
  readonly previousTotal = this._previousTotal.asReadonly();

  /** Kalan saniye (0 alt sinirli). Hold yoksa `null`. */
  readonly remainingSeconds = computed<number | null>(() => {
    const hold = this.hold();
    if (hold === null) {
      return null;
    }
    const expiresAt = Date.parse(hold.expiresAt);
    if (Number.isNaN(expiresAt)) {
      return hold.expiresInSeconds;
    }
    return Math.max(0, Math.floor((expiresAt - this._now()) / 1000));
  });

  /** Sure doldu mu? Sunucu 409 dondurmeden once istemci bunu bilir. */
  readonly expired = computed(() => this._expired() || this.remainingSeconds() === 0);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopTimer());
  }

  /**
   * Teklifi dondur (arama sonucundan "sec").
   * `onSuccess` verilirse yeni hold ile cagrilir — cagiran sayfa rezervasyon
   * adimina gecebilsin diye. Basarisizlikta cagrilmaz; hata `error()`'dadir.
   */
  create(request: PublicCreateHoldRequest, onSuccess?: (hold: PublicHold) => void): void {
    if (this.loading()) {
      return; // Cift tiklama: ayni oda icin iki hold acilmaz.
    }
    this.slot.begin();
    this._expired.set(false);
    this.api.createHold(request).subscribe({
      next: (hold) => {
        this.accept(hold);
        onSuccess?.(hold);
      },
      error: (error: unknown) => this.reject(error),
    });
  }

  /** Sayfa yenilendiginde: donmus teklifi ve kalan sureyi yeniden oku. */
  open(token: string): void {
    if (this.hold()?.holdToken === token && this.slot.ready()) {
      return;
    }
    this.slot.begin();
    this._expired.set(false);
    this.api.getHold(token).subscribe({
      next: (hold) => this.accept(hold),
      error: (error: unknown) => this.reject(error),
    });
  }

  /**
   * KURTARMA: sure dolduysa ayni parametrelerle **yeni** teklif alinir.
   * Onceki toplam saklanir ki ekran "fiyat degisti" diyebilsin.
   */
  renew(): void {
    const hold = this.hold();
    const request: PublicCreateHoldRequest | null =
      hold === null
        ? null
        : {
            roomTypeCode: hold.roomTypeCode,
            checkIn: hold.checkIn,
            checkOut: hold.checkOut,
            adults: hold.adults,
            children: hold.children,
          };

    if (request === null) {
      return;
    }
    this._previousTotal.set(hold?.price.totalGross ?? null);
    this.create(request);
  }

  /** Yenileme icin parametreler disaridan da verilebilir (hold okunamadiysa). */
  renewWith(request: PublicCreateHoldRequest, previousTotal: number | null = null): void {
    this._previousTotal.set(previousTotal);
    this.create(request);
  }

  /**
   * Misafir akistan cikarsa envanter **hemen** birakilir. Uc idempotenttir;
   * hata yok sayilir — kullanicinin gorecegi bir sey yoktur.
   */
  release(): void {
    const token = this.hold()?.holdToken ?? this.storedToken();
    this.clear();
    if (token !== null) {
      this.api.releaseHold(token).subscribe({ error: () => undefined });
    }
  }

  /** Rezervasyon basarili olduktan sonra: hold tuketildi, izini birakma. */
  clear(): void {
    this.stopTimer();
    this.slot.reset();
    this._expired.set(false);
    this._previousTotal.set(null);
    this.storage.session?.removeItem(HOLD_STORAGE_KEY);
  }

  /** Adres cubugunda token yoksa oturum deposundan kurtar. */
  storedToken(): string | null {
    return this.storage.session?.getItem(HOLD_STORAGE_KEY) ?? null;
  }

  /** Ozet degistiginde (409 SUMMARY_CHANGED) donmus teklifi tazeler. */
  refresh(): void {
    const token = this.hold()?.holdToken ?? this.storedToken();
    if (token === null) {
      return;
    }
    this._previousTotal.set(this.hold()?.price.totalGross ?? null);
    this.slot.begin();
    this.api.getHold(token).subscribe({
      next: (hold) => this.accept(hold),
      error: (error: unknown) => this.reject(error),
    });
  }

  private accept(hold: PublicHold): void {
    this.slot.succeed(hold);
    this.storage.session?.setItem(HOLD_STORAGE_KEY, hold.holdToken);
    this.startTimer();
  }

  private reject(error: unknown): void {
    const mapped: PublicApiError = toPublicError(error);
    this.stopTimer();
    if (mapped.code === 'HOLD_EXPIRED' || mapped.code === 'HOLD_NOT_FOUND') {
      this._expired.set(true);
    }
    this.slot.fail(mapped);
  }

  private startTimer(): void {
    if (!this.storage.browser || this.timer !== null) {
      return;
    }
    this._now.set(Date.now());
    this.timer = setInterval(() => {
      this._now.set(Date.now());
      if (this.remainingSeconds() === 0) {
        this._expired.set(true);
        this.stopTimer();
      }
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
