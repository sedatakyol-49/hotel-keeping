import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { fieldErrorList, type PublicApiError } from '../../../core/api/public-error';
import { Notice } from '../notice/notice';

/**
 * Sunucu hatalarinin **tek** gorunumu.
 *
 * KURAL: misafir hicbir zaman `HOLD_EXPIRED` ya da `409` gormez. Her hata uc
 * parcaya cevrilir — ne oldu (baslik), ne anlama geliyor (govde), **ne
 * yapmali** (tek dugme). Ham kod yalnizca `data-error-code` niteliginde durur:
 * destek ekibi ekran goruntusunden okuyabilsin, misafir ekranda gormesin.
 *
 * Kurtarma eylemi `error.recovery` alanindan gelir (bkz. public-error.ts), yani
 * her ekranda ayni koda ayni cikis yolu onerilir.
 */
@Component({
  selector: 'hcg-error-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Notice],
  template: `
    <div [attr.data-error-code]="error().code" data-testid="error-panel">
      <hcg-notice
        [tone]="tone()"
        [assertive]="true"
        [label]="'errors.public.label' | translate"
        [heading]="error().titleKey | translate"
      >
        <p data-testid="error-body">{{ error().bodyKey | translate }}</p>

        @if (error().retryAfterSeconds !== null) {
          <p class="mt-2 numeric text-xs" data-testid="error-retry-after">
            {{ 'errors.public.retryAfter' | translate: { seconds: error().retryAfterSeconds } }}
          </p>
        }

        @if (fieldMessages().length > 0) {
          <ul class="mt-2 grid gap-1 text-xs" data-testid="error-fields">
            @for (message of fieldMessages(); track message) {
              <li>{{ message }}</li>
            }
          </ul>
        }

        <div data-notice-actions class="mt-4 flex flex-wrap gap-3">
          @if (error().recovery !== 'none') {
            <button
              type="button"
              class="hcg-action hcg-action--quiet"
              data-testid="error-action"
              (click)="recover.emit()"
            >
              {{ 'errors.recovery.' + error().recovery | translate }}
            </button>
          }
          <ng-content select="[data-error-extra]" />
        </div>
      </hcg-notice>
    </div>
  `,
})
export class ErrorPanel {
  readonly error = input.required<PublicApiError>();
  readonly recover = output<void>();

  protected readonly fieldMessages = computed(() => fieldErrorList(this.error()));

  /** Dogrulama hatalari uyaridir; kalanlar gercek hata. */
  protected readonly tone = computed(() =>
    this.error().code === 'VALIDATION_FAILED' ? 'warning' : 'danger',
  );
}
