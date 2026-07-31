import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/**
 * Onay kutusu.
 *
 * DSGVO Art. 4 Nr. 11 / EuGH C-673/17 (Planet49): onay **etkin bir eylemle**
 * verilmelidir. Bu yuzden:
 *   - `checked` girdisi **varsayilan olarak `false`**'tur ve bileşen kendi
 *     basina hicbir kutuyu isaretlemez,
 *   - "hepsini kabul et" gibi toplu bir kutu yoktur; her onay ayri ve
 *     kendi metniyle verilir (AGB, aydinlatma, cayma bildirimi, 18+).
 *
 * Dokunmatik hedef: etiketin tamami tiklanabilir ve satir yuksekligi 44px'in
 * ustundedir (`touch-target`). Kutunun kendisi 1rem; asil hedef etikettir.
 */
@Component({
  selector: 'hcg-check-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid gap-1">
      <label
        class="flex touch-target cursor-pointer items-start gap-3 py-1.5"
        [attr.for]="fieldId()"
      >
        <input
          type="checkbox"
          class="mt-0.5 size-4 shrink-0 accent-[var(--color-navy)]"
          [id]="fieldId()"
          [attr.name]="name()"
          [checked]="checked()"
          [attr.aria-invalid]="error() ? 'true' : null"
          [attr.aria-describedby]="describedBy()"
          (change)="toggle($event)"
          [attr.data-testid]="'check-' + name()"
        />
        <span class="text-sm">
          <ng-content />
        </span>
      </label>

      @if (error(); as message) {
        <p class="text-xs text-danger" [id]="errorId()" [attr.data-testid]="'error-' + name()">
          {{ message }}
        </p>
      }
    </div>
  `,
})
export class CheckField {
  readonly name = input.required<string>();
  /** Varsayilan `false` — on isaretli kutu yasaktir. */
  readonly checked = input(false);
  readonly error = input<string | null>(null);

  readonly checkedChange = output<boolean>();

  protected readonly fieldId = computed(() => `check-${this.name()}`);
  protected readonly errorId = computed(() => `error-${this.name()}`);
  protected readonly describedBy = computed(() => (this.error() ? this.errorId() : null));

  protected toggle(event: Event): void {
    this.checkedChange.emit((event.target as HTMLInputElement).checked);
  }
}
