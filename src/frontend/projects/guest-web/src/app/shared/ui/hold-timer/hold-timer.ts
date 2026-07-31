import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { formatCountdown } from '@hotelcore/shared';

/**
 * ===========================================================================
 * HOLD GERI SAYIMI — gorunur, ama bagirmayan
 * ===========================================================================
 *
 * Misafirin 15 dakikasi var (mimari §5.2). Uc sey ZORUNLU:
 *  1) Kalan sure **gorunur** olmali,
 *  2) Sure dolunca ne olacagi **onceden** yazmali ("oda birakilir, fiyat
 *     yeniden hesaplanir"), sonradan surpriz olmamali,
 *  3) Dolunca **kurtarma yolu** olmali (yeni teklif) — bunu ust bilesen sunar.
 *
 * ERISILEBILIRLIK KARARI (onemli):
 * Geri sayim `aria-live` ile isaretlenirse ekran okuyucu **saniyede bir**
 * konusur ve sayfayi kullanilamaz hale getirir. Dogru cozum:
 *   - Sayacin kendisi `role="timer"` + `aria-live="off"`: yardimci teknoloji
 *     ogeyi bir zamanlayici olarak tanir, kullanici isterse okur; kendiliginden
 *     okumaz.
 *   - Ayri bir `aria-live="polite"` bolgesi yalnizca **esiklerde** degisir
 *     (5 dakika, 1 dakika, sure doldu). Uc cumle, saniyede bir degil.
 *
 * Gorsel: rakamlar mono/tabular; her saniye genislik degistirip satiri
 * titretmemeleri icin.
 */
@Component({
  selector: 'hcg-hold-timer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div
      class="flex flex-wrap items-baseline gap-x-4 gap-y-1 border border-rule bg-paper-raised px-4 py-3"
      data-testid="hold-timer"
    >
      <p class="eyebrow">{{ 'hold.remaining' | translate }}</p>

      <p
        class="numeric text-xl"
        role="timer"
        aria-live="off"
        [attr.aria-label]="ariaLabel()"
        data-testid="hold-timer-value"
      >
        {{ display() }}
      </p>

      <p class="w-full text-xs text-ink-muted" data-testid="hold-timer-hint">
        {{ (expired() ? 'hold.expiredHint' : 'hold.hint') | translate }}
      </p>
    </div>

    <!--
      Seyrek duyuru bolgesi: icerik yalnizca esik gecildiginde degisir.
      Bos oldugu surece ekran okuyucu hicbir sey soylemez.
    -->
    <p class="sr-only" aria-live="polite" data-testid="hold-timer-announcement">
      {{ announcement() }}
    </p>
  `,
})
export class HoldTimer {
  /** Kalan saniye; `null` ise hold yok (sayac cizgi gosterir). */
  readonly seconds = input<number | null>(null);
  readonly expired = input(false);

  private readonly translate = inject(TranslateService);

  protected readonly display = computed(() => {
    const seconds = this.seconds();
    return seconds === null ? '––:––' : formatCountdown(this.expired() ? 0 : seconds);
  });

  protected readonly ariaLabel = computed(() => {
    const seconds = this.seconds();
    if (seconds === null || this.expired()) {
      return this.text('hold.a11y.expired');
    }
    return this.text('hold.a11y.remaining', {
      minutes: Math.floor(seconds / 60),
      seconds: seconds % 60,
    });
  });

  /**
   * Duyuru **kovalar** halinde uretilir: `>5dk` bos, `<=5dk`, `<=1dk`, `0`.
   * Kova degismedigi surece metin ayni kalir ve live-region tetiklenmez.
   */
  protected readonly announcement = computed(() => {
    const seconds = this.seconds();
    if (seconds === null) {
      return '';
    }
    if (this.expired() || seconds === 0) {
      return this.text('hold.a11y.announceExpired');
    }
    if (seconds <= 60) {
      return this.text('hold.a11y.announceOneMinute');
    }
    if (seconds <= 300) {
      return this.text('hold.a11y.announceFiveMinutes');
    }
    return '';
  });

  private text(key: string, params?: Record<string, unknown>): string {
    // Dil degisiminde yeniden hesaplansin diye aktif dil okunur.
    this.translate.currentLang();
    const value: unknown = this.translate.instant(key, params);
    return typeof value === 'string' ? value : key;
  }
}
