import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { ManageBookingStore } from '../../core/state/manage-booking.store';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { ErrorSummary, type FieldProblem } from '../../shared/ui/form/error-summary';
import { TextField } from '../../shared/ui/form/text-field';
import { Notice } from '../../shared/ui/notice/notice';
import { PageIntro } from '../../shared/ui/page-intro/page-intro';

/**
 * ===========================================================================
 * REZERVASYON SORGULAMA (baglanti yenileme)
 * ===========================================================================
 *
 * Sozlesme §7.4: bu uc **hicbir kosulda veri dondurmez** ve eslesme olsun ya da
 * olmasin **202** doner. Sebep numaralandirma (enumeration) korumasidir: yanit
 * gövdesi ya da suresi bir rezervasyonun varligini sizdirmamalidir.
 *
 * ARAYUZ BUNA UYMAK ZORUNDA. "Rezervasyon bulundu, e-posta gonderildi" demek,
 * sunucunun ozenle gizledigi bilgiyi ekranda ifsa ederdi. Bu yuzden mesaj
 * **kosulludur**: "Bu bilgilerle bir rezervasyon varsa, erisim baglantisi
 * e-posta adresine gonderildi." Kullanici acisindan da dogru bilgidir.
 *
 * Referans biçimi (Crockford Base32, `4-4-4`) sunucuda normalize edilir;
 * istemci yalnizca bosluklari kirpar, "duzeltme" yapmaz.
 */
@Component({
  selector: 'hcg-manage-lookup-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro, TextField, ErrorSummary, ErrorPanel, Notice],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'manage.eyebrow' | translate"
        [heading]="'manage.lookup.title' | translate"
        [lede]="'manage.lookup.lede' | translate"
      />

      @if (store.lookupSent()) {
        <div class="mt-10 max-w-measure" data-testid="lookup-sent">
          <hcg-notice tone="success" [heading]="'manage.lookup.sentTitle' | translate">
            <p>{{ 'manage.lookup.sentBody' | translate }}</p>
            <div data-notice-actions class="mt-4">
              <button type="button" class="hcg-action hcg-action--quiet" (click)="again()">
                {{ 'manage.lookup.again' | translate }}
              </button>
            </div>
          </hcg-notice>
        </div>
      } @else {
        <form
          class="mt-10 max-w-measure"
          novalidate
          (submit)="submit($event)"
          data-testid="lookup-form"
        >
          <hcg-error-summary [problems]="problems()" />

          @if (store.lookupError(); as error) {
            <div class="mb-6">
              <hcg-error-panel [error]="error" (recover)="retry()" />
            </div>
          }

          <div class="grid gap-5">
            <hcg-text-field
              name="bookingReference"
              [label]="'manage.lookup.reference' | translate"
              [hint]="'manage.lookup.referenceHint' | translate"
              [value]="reference()"
              [required]="true"
              [requiredText]="'form.required' | translate"
              [maxLength]="20"
              [error]="problemFor('bookingReference')"
              (valueChange)="reference.set($event)"
            />
            <hcg-text-field
              name="email"
              type="email"
              inputMode="email"
              [label]="'manage.lookup.email' | translate"
              [value]="email()"
              [required]="true"
              [requiredText]="'form.required' | translate"
              autocomplete="email"
              [maxLength]="256"
              [error]="problemFor('email')"
              (valueChange)="email.set($event)"
            />
          </div>

          <button
            type="submit"
            class="hcg-action mt-8"
            [disabled]="store.lookupPending()"
            data-testid="lookup-submit"
          >
            {{ (store.lookupPending() ? 'manage.lookup.sending' : 'manage.lookup.submit') | translate }}
          </button>

          <p class="mt-6 text-xs text-ink-faint" data-testid="lookup-privacy-note">
            {{ 'manage.lookup.privacyNote' | translate }}
          </p>
        </form>
      }
    </div>
  `,
})
export class ManageLookupPage {
  protected readonly store = inject(ManageBookingStore);
  private readonly translate = inject(TranslateService);

  protected readonly reference = signal('');
  protected readonly email = signal('');
  protected readonly problems = signal<readonly FieldProblem[]>([]);

  protected submit(event: Event): void {
    event.preventDefault();

    const problems: FieldProblem[] = [];
    if (this.reference().trim().length === 0) {
      problems.push({ field: 'bookingReference', message: this.text('form.errors.required') });
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(this.email().trim())) {
      problems.push({ field: 'email', message: this.text('form.errors.email') });
    }
    this.problems.set(problems);
    if (problems.length > 0) {
      return;
    }

    this.store.lookup(this.reference().trim(), this.email().trim());
  }

  protected problemFor(field: string): string | null {
    return this.problems().find((problem) => problem.field === field)?.message ?? null;
  }

  protected again(): void {
    this.store.resetLookup();
  }

  protected retry(): void {
    this.store.resetLookup();
  }

  private text(key: string): string {
    const value: unknown = this.translate.instant(key);
    return typeof value === 'string' ? value : key;
  }
}
