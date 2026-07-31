import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/**
 * Metin girdisi — etiket, ipucu ve hata mesaji **tek** yerde baglanir.
 *
 * NEDEN ICERIK YANSITMA (content projection) DEGIL: `<input>` disaridan
 * verilseydi `id`, `aria-describedby` ve `aria-invalid` baglantilarini her
 * sayfanin elle kurmasi gerekirdi; biri unutuldugunda ekran okuyucu hatayi
 * hic duyurmaz. Girdi burada uretilir, baglantilar yapisal olarak garanti
 * altindadir.
 *
 * Dokunmatik: `.hc-input` min-height 2.75rem (44px) tasir; etiket ayri satirda
 * ve tiklanabilir (`for`), yani hedef alani daha da buyuktur.
 *
 * Sayisal alanlarda `inputmode="numeric"` verilir — mobil klavye dogru acilir.
 */
@Component({
  selector: 'hcg-text-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-1.5">
      <label class="hc-label" [attr.for]="fieldId()">
        {{ label() }}
        @if (required()) {
          <span aria-hidden="true">*</span>
          <span class="sr-only">{{ requiredText() }}</span>
        }
      </label>

      @if (multiline()) {
        <textarea
          class="hc-input"
          [id]="fieldId()"
          [attr.name]="name()"
          [attr.rows]="rows()"
          [attr.maxlength]="maxLength()"
          [attr.required]="required() ? '' : null"
          [attr.aria-invalid]="error() ? 'true' : null"
          [attr.aria-describedby]="describedBy()"
          [attr.autocomplete]="autocomplete()"
          [value]="value()"
          (input)="emit($event)"
          [attr.data-testid]="'field-' + name()"
        ></textarea>
      } @else {
        <input
          class="hc-input"
          [id]="fieldId()"
          [attr.name]="name()"
          [type]="type()"
          [attr.inputmode]="inputMode()"
          [attr.maxlength]="maxLength()"
          [attr.placeholder]="placeholder() || null"
          [attr.required]="required() ? '' : null"
          [attr.aria-invalid]="error() ? 'true' : null"
          [attr.aria-describedby]="describedBy()"
          [attr.autocomplete]="autocomplete()"
          [attr.min]="min() || null"
          [attr.max]="max() || null"
          [value]="value()"
          (input)="emit($event)"
          [attr.data-testid]="'field-' + name()"
        />
      }

      @if (hint()) {
        <p class="text-xs text-ink-faint" [id]="hintId()">{{ hint() }}</p>
      }
      @if (error(); as message) {
        <p
          class="text-xs text-danger"
          [id]="errorId()"
          [attr.data-testid]="'error-' + name()"
        >
          {{ message }}
        </p>
      }
    </div>
  `,
})
export class TextField {
  readonly name = input.required<string>();
  readonly label = input.required<string>();
  readonly value = input('');
  readonly type = input<'text' | 'email' | 'tel' | 'time' | 'date' | 'number'>('text');
  readonly inputMode = input<string | null>(null);
  readonly autocomplete = input<string | null>(null);
  readonly placeholder = input('');
  readonly hint = input('');
  readonly error = input<string | null>(null);
  readonly required = input(false);
  readonly requiredText = input('');
  readonly maxLength = input<number | null>(null);
  readonly multiline = input(false);
  readonly rows = input(3);
  readonly min = input('');
  readonly max = input('');

  readonly valueChange = output<string>();

  protected readonly fieldId = computed(() => `field-${this.name()}`);
  protected readonly hintId = computed(() => `hint-${this.name()}`);
  protected readonly errorId = computed(() => `error-${this.name()}`);

  protected readonly describedBy = computed(() => {
    const ids = [this.hint() ? this.hintId() : null, this.error() ? this.errorId() : null].filter(
      (id): id is string => id !== null,
    );
    return ids.length > 0 ? ids.join(' ') : null;
  });

  protected emit(event: Event): void {
    const target = event.target as HTMLInputElement | HTMLTextAreaElement;
    this.valueChange.emit(target.value);
  }
}
