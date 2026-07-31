import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

export interface FieldProblem {
  /** Hedef alanin `name`'i — baglanti `#field-{name}` olur. */
  readonly field: string;
  readonly message: string;
  /** Onay kutulari `#check-{name}` id'si tasir. */
  readonly kind?: 'input' | 'check';
}

/**
 * Form hata ozeti (WCAG 3.3.1 / 3.3.3).
 *
 * Uzun bir formda gonderim reddedildiginde kullanicinin yapamayacagi sey,
 * hangi alanin bozuk oldugunu **aramaktir**. Ozet formun basinda durur,
 * `role="alert"` ile duyurulur ve her satir ilgili alana **baglanti**dir:
 * tiklayinca odak alana gider (klavye ve ekran okuyucu icin ayni yol).
 *
 * Odak yonetimi: ozet gorunur oldugunda konteynere odak verilir; boylece ekran
 * okuyucu listenin basindan okur ve klavye kullanicisi "Tab" ile ilk bozuk
 * alana ulasir.
 */
@Component({
  selector: 'hcg-error-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    @if (problems().length > 0) {
      <div
        class="border border-rule border-l-2 bg-paper-raised px-4 py-4"
        style="border-left-color: var(--color-danger)"
        role="alert"
        tabindex="-1"
        data-testid="error-summary"
      >
        <p class="label-mono text-danger">
          {{ 'form.errorSummary' | translate: { count: problems().length } }}
        </p>
        <ul class="mt-2 grid gap-1">
          @for (problem of problems(); track problem.field) {
            <li class="text-sm">
              <a
                [href]="'#' + anchor(problem)"
                class="underline underline-offset-2"
                [attr.data-testid]="'summary-link-' + problem.field"
              >
                {{ problem.message }}
              </a>
            </li>
          }
        </ul>
      </div>
    }
  `,
})
export class ErrorSummary {
  readonly problems = input.required<readonly FieldProblem[]>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    effect(() => {
      if (this.problems().length === 0) {
        return;
      }
      const container = this.host.nativeElement.querySelector<HTMLElement>(
        '[data-testid="error-summary"]',
      );
      container?.focus({ preventScroll: false });
    });
  }

  protected anchor(problem: FieldProblem): string {
    return problem.kind === 'check' ? `check-${problem.field}` : `field-${problem.field}`;
  }
}
