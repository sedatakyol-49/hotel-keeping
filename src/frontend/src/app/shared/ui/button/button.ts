import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'md' | 'sm';

/**
 * Defter estetigine uygun buton: kose yuvarlama ve golge yok, 1px cetvel cerceve,
 * mono + uppercase etiket. Dokunmatik hedef >= 44px.
 */
@Component({
  selector: 'hc-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled() || busy()"
      [attr.aria-busy]="busy() ? 'true' : null"
      [class]="classes()"
      (click)="pressed.emit($event)"
    >
      <ng-content />
    </button>
  `,
  styles: `
    :host {
      display: inline-flex;
    }
    :host([block]) {
      display: flex;
      width: 100%;
    }
  `,
})
export class Button {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input(false);
  /** Bekleme durumu: buton kilitlenir ve `aria-busy` isaretlenir. */
  readonly busy = input(false);
  readonly block = input(false);

  readonly pressed = output<MouseEvent>();

  protected readonly classes = computed(() => {
    const base =
      'inline-flex items-center justify-center gap-2 border label-mono transition-colors ' +
      'disabled:cursor-not-allowed disabled:opacity-50';
    const sizing = this.size() === 'sm' ? 'min-h-9 px-3 py-1.5' : 'min-h-touch px-4 py-2';
    const width = this.block() ? 'w-full' : '';

    const variants: Record<ButtonVariant, string> = {
      primary: 'bg-navy text-paper border-navy hover:bg-ink hover:border-ink',
      secondary: 'bg-transparent text-ink border-rule-strong hover:bg-paper-sunken',
      ghost:
        'bg-transparent text-ink-muted border-transparent hover:text-ink hover:bg-paper-sunken',
      danger: 'bg-transparent text-danger border-danger hover:bg-danger hover:text-paper',
    };

    return [base, sizing, width, variants[this.variant()]].filter(Boolean).join(' ');
  });
}
