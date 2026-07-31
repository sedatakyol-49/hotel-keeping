import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import type { PublicAvailabilityQuery } from '../../../core/api/public-models';
import { addDays, nightsBetween, todayIso } from '../../../core/dates/stay-dates';

/**
 * Arama formu — sitenin **tek** giris noktasi.
 *
 * TASARIM:
 *  - Dort alan tek satirda (masaustu), alt alta (mobil). Ayrilar 1px cetvel;
 *    kutu golgesi ya da yuvarlak kose yok.
 *  - Tarih alanlari **native** `<input type="date">`: kendi takvim bileşenimizi
 *    yazmak, mobilde isletim sisteminin secicisinden daha kotu bir sey uretmek
 *    demektir; ayrica klavye ve ekran okuyucu davranisi bedava gelir.
 *  - Kisi sayilari `<select>`: ust sinir otelden gelir (`maxAdults`,
 *    `maxChildren`), yani gecersiz bir deger secilemez. Serbest sayi girisi
 *    olsaydi "12 yetiskin" yazip 409 almak mumkun olurdu.
 *
 * DOGRULAMA: sunucu kurallarinin **kopyasi degildir**, yalnizca acik hatalari
 * gonderim oncesi yakalar (cikis <= giris, gecmis tarih). Asil dogrulama
 * hold ucundadir (sozlesme §6.3) ve oradan gelen hata ekranda gosterilir.
 */
@Component({
  selector: 'hcg-search-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <form
      class="border border-rule bg-paper-raised"
      (submit)="submit($event)"
      [attr.aria-label]="'search.form.label' | translate"
      data-testid="search-form"
    >
      <div class="grid gap-px bg-rule md:grid-cols-[1fr_1fr_auto_auto]">
        <div class="bg-paper-raised p-4">
          <label class="hc-label" for="search-check-in">
            {{ 'search.form.checkIn' | translate }}
          </label>
          <input
            id="search-check-in"
            class="hc-input mt-1.5"
            type="date"
            name="checkIn"
            [value]="checkIn()"
            [attr.min]="minDate()"
            (change)="onCheckIn($event)"
            data-testid="search-check-in"
          />
        </div>

        <div class="bg-paper-raised p-4">
          <label class="hc-label" for="search-check-out">
            {{ 'search.form.checkOut' | translate }}
          </label>
          <input
            id="search-check-out"
            class="hc-input mt-1.5"
            type="date"
            name="checkOut"
            [value]="checkOut()"
            [attr.min]="minCheckOut()"
            (change)="onCheckOut($event)"
            data-testid="search-check-out"
          />
        </div>

        <div class="grid grid-cols-2 gap-px bg-rule">
          <div class="bg-paper-raised p-4">
            <label class="hc-label" for="search-adults">
              {{ 'search.form.adults' | translate }}
            </label>
            <select
              id="search-adults"
              class="hc-input mt-1.5"
              name="adults"
              [value]="adults()"
              (change)="onAdults($event)"
              data-testid="search-adults"
            >
              @for (option of adultOptions(); track option) {
                <option [value]="option" [selected]="option === adults()">{{ option }}</option>
              }
            </select>
          </div>

          <div class="bg-paper-raised p-4">
            <label class="hc-label" for="search-children">
              {{ 'search.form.children' | translate }}
            </label>
            <select
              id="search-children"
              class="hc-input mt-1.5"
              name="children"
              [value]="children()"
              (change)="onChildren($event)"
              data-testid="search-children"
            >
              @for (option of childOptions(); track option) {
                <option [value]="option" [selected]="option === children()">{{ option }}</option>
              }
            </select>
          </div>
        </div>

        <div class="flex items-stretch bg-paper-raised p-4">
          <button type="submit" class="hcg-action w-full md:w-auto" data-testid="search-submit">
            {{ 'search.form.submit' | translate }}
          </button>
        </div>
      </div>

      @if (problem(); as key) {
        <p class="border-t border-rule px-4 py-2 text-sm text-danger" role="alert" data-testid="search-form-error">
          {{ key | translate }}
        </p>
      }

      <p class="border-t border-rule px-4 py-2 text-xs text-ink-faint" data-testid="search-form-note">
        {{ 'search.form.priceNote' | translate }}
      </p>
    </form>
  `,
})
export class SearchForm {
  readonly initial = input<PublicAvailabilityQuery | null>(null);
  readonly maxAdults = input(6);
  readonly maxChildren = input(6);

  /* `search` DEGIL: DOM `search` olayiyla cakisir (lint kurali). */
  readonly submitted = output<PublicAvailabilityQuery>();

  private readonly _checkIn = signal('');
  private readonly _checkOut = signal('');
  private readonly _adults = signal(2);
  private readonly _children = signal(0);
  private readonly _problem = signal<string | null>(null);
  /** Girdiye dokunulduysa `initial` degisimi degeri ezmez. */
  private readonly touched = signal(false);

  protected readonly checkIn = computed(() =>
    this.touched() ? this._checkIn() : (this.initial()?.checkIn ?? this._checkIn()),
  );
  protected readonly checkOut = computed(() =>
    this.touched() ? this._checkOut() : (this.initial()?.checkOut ?? this._checkOut()),
  );
  protected readonly adults = computed(() =>
    this.touched() ? this._adults() : (this.initial()?.adults ?? this._adults()),
  );
  protected readonly children = computed(() =>
    this.touched() ? this._children() : (this.initial()?.children ?? this._children()),
  );

  protected readonly problem = this._problem.asReadonly();
  protected readonly minDate = computed(() => todayIso());
  protected readonly minCheckOut = computed(() => {
    const checkIn = this.checkIn();
    return checkIn.length > 0 ? addDays(checkIn, 1) : todayIso();
  });

  protected readonly adultOptions = computed(() =>
    Array.from({ length: Math.max(1, this.maxAdults()) }, (_, index) => index + 1),
  );
  protected readonly childOptions = computed(() =>
    Array.from({ length: Math.max(0, this.maxChildren()) + 1 }, (_, index) => index),
  );

  protected onCheckIn(event: Event): void {
    this.snapshot();
    const value = (event.target as HTMLInputElement).value;
    this._checkIn.set(value);

    /* Cikis girisin gerisinde kaldiysa bir gece ileri tasinir — kullaniciya
       "gecersiz" demektense makul olani yapmak daha iyidir. */
    const checkOut = this._checkOut();
    if (value.length > 0 && (checkOut.length === 0 || checkOut <= value)) {
      this._checkOut.set(addDays(value, 1));
    }
  }

  protected onCheckOut(event: Event): void {
    this.snapshot();
    this._checkOut.set((event.target as HTMLInputElement).value);
  }

  protected onAdults(event: Event): void {
    this.snapshot();
    this._adults.set(Number((event.target as HTMLSelectElement).value));
  }

  protected onChildren(event: Event): void {
    this.snapshot();
    this._children.set(Number((event.target as HTMLSelectElement).value));
  }

  protected submit(event: Event): void {
    event.preventDefault();

    const query: PublicAvailabilityQuery = {
      checkIn: this.checkIn(),
      checkOut: this.checkOut(),
      adults: this.adults(),
      children: this.children(),
    };

    if (query.checkIn.length === 0 || query.checkOut.length === 0) {
      this._problem.set('search.form.errors.missingDates');
      return;
    }
    if (query.checkIn < todayIso()) {
      this._problem.set('search.form.errors.pastDate');
      return;
    }
    if (nightsBetween(query.checkIn, query.checkOut) < 1) {
      this._problem.set('search.form.errors.order');
      return;
    }

    this._problem.set(null);
    this.submitted.emit(query);
  }

  /** Ilk dokunusta `initial` degerleri yerel duruma kopyalanir. */
  private snapshot(): void {
    if (!this.touched()) {
      this._checkIn.set(this.checkIn());
      this._checkOut.set(this.checkOut());
      this._adults.set(this.adults());
      this._children.set(this.children());
      this.touched.set(true);
    }
  }
}
