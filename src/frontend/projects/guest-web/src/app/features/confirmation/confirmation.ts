import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore } from '@hotelcore/shared';

import { languagePath } from '../../core/i18n/language-url';
import { BookingStore } from '../../core/state/booking.store';
import { ManageBookingStore } from '../../core/state/manage-booking.store';
import { BookingView } from '../../shared/ui/booking-view/booking-view';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { Notice } from '../../shared/ui/notice/notice';

/**
 * ===========================================================================
 * ONAY EKRANI
 * ===========================================================================
 *
 * §312f BGB: sozlesme onayinin **kalici bir veri tasiyicisinda** verilmesi
 * gerekir. Bunu yapan e-postadir (icerik gövdede, mimari §9.8). Ekranin gorevi,
 * bunu misafire **soylemektir**: "onay e-postasi ... adresine gonderildi".
 * Sessizce gecmek, misafirin belgeyi arayacagi yeri bilmemesine yol acar.
 *
 * ADRESTEKI DEGER `accessToken`'dir (`bookingReference` degil). Sebep sozlesme
 * §7.1: referans **tasiyici kimlik bilgisi degildir** ve tek basina veri
 * dondurmez; token ise dondurur. Sayfa yenilendiginde veri token ile yeniden
 * cekilir, boylece "F5 = bos ekran" olmaz.
 *
 * Render modu **istemci** ve `noindex, nofollow`.
 */
@Component({
  selector: 'hcg-confirmation-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, BookingView, ErrorPanel, Notice],
  template: `
    <div class="hcg-shell py-12">
      @if (booking(); as result) {
        <p class="eyebrow">{{ 'confirmation.eyebrow' | translate }}</p>
        <h1 class="mt-3 text-headline">{{ 'confirmation.title' | translate }}</h1>

        <!-- §312f: kalici veri tasiyicisi bildirimi -->
        <div class="mt-6 max-w-measure">
          <hcg-notice
            tone="success"
            [label]="'confirmation.documentLabel' | translate"
            [heading]="'confirmation.emailTitle' | translate"
          >
            <p data-testid="confirmation-email">
              {{
                'confirmation.emailBody'
                  | translate: { recipient: result.confirmation.recipientMasked }
              }}
            </p>
            <p class="numeric mt-2 text-xs" data-testid="confirmation-document-version">
              {{
                'confirmation.documentVersion'
                  | translate
                    : {
                        version: result.confirmation.documentVersion,
                        culture: result.confirmation.culture,
                      }
              }}
            </p>
          </hcg-notice>
        </div>

        <div class="mt-10">
          <hcg-booking-view [booking]="result" />
        </div>

        <div class="mt-10 flex flex-wrap gap-4 border-t border-rule pt-8">
          @if (manageLink(); as link) {
            <a [routerLink]="link" class="hcg-action" data-testid="confirmation-manage">
              {{ 'confirmation.manage' | translate }}
            </a>
          }
          <a [routerLink]="homePath()" class="hcg-action hcg-action--quiet">
            {{ 'confirmation.home' | translate }}
          </a>
        </div>

        <p class="mt-6 max-w-measure text-xs text-ink-faint" data-testid="confirmation-link-note">
          {{ 'confirmation.linkNote' | translate }}
        </p>
      } @else if (manage.error(); as error) {
        <p class="eyebrow">{{ 'confirmation.eyebrow' | translate }}</p>
        <h1 class="mt-3 text-headline">{{ 'confirmation.missing.title' | translate }}</h1>
        <div class="mt-8 max-w-measure">
          <hcg-error-panel [error]="error" (recover)="reload()">
            <a data-error-extra [routerLink]="lookupPath()" class="hcg-action">
              {{ 'manage.lookup.cta' | translate }}
            </a>
          </hcg-error-panel>
        </div>
      } @else {
        <h1 class="text-headline">{{ 'confirmation.title' | translate }}</h1>
        <p class="mt-6 label-mono text-ink-muted" role="status" data-testid="confirmation-loading">
          {{ 'common.loading' | translate }}
        </p>
      }
    </div>
  `,
})
export class ConfirmationPage {
  /** `/{lang}/confirmation/:token` — token, erisim kimlik bilgisidir. */
  readonly token = input.required<string>();

  private readonly created = inject(BookingStore);
  protected readonly manage = inject(ManageBookingStore);
  private readonly language = inject(LanguageStore);
  private readonly loaded = signal('');

  /**
   * Once az once olusturulan rezervasyon (tek istek, taze veri); sayfa
   * yenilendiyse token ile sunucudan cekilen kayit.
   */
  protected readonly booking = computed(() => this.created.booking() ?? this.manage.booking());

  protected readonly manageLink = computed(() => {
    const token = this.token();
    return token.length > 0 ? languagePath(this.language.current(), 'manage', token) : null;
  });

  constructor() {
    effect(() => {
      const token = this.token();
      if (token.length === 0 || this.loaded() === token) {
        return;
      }
      /* Olusturma yanitini elimizde tutuyorsak ikinci bir istek atmayiz. */
      if (this.created.booking()?.accessToken === token) {
        this.loaded.set(token);
        return;
      }
      this.loaded.set(token);
      this.manage.load(token);
    });
  }

  protected reload(): void {
    this.manage.load(this.token());
  }

  protected homePath(): string {
    return languagePath(this.language.current());
  }

  protected lookupPath(): string {
    return languagePath(this.language.current(), 'manage');
  }
}
