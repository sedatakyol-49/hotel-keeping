import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type BadgeTone = 'neutral' | 'navy' | 'copper' | 'brass' | 'danger' | 'success';

/**
 * Durum rozeti — pill degil, kare kenarli 1px cetvel cerceve.
 * `brass` tonu opsiyon/bekleyen durumlar icin ayrilmistir.
 */
@Component({
  selector: 'hc-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="classes()">
      <ng-content />
    </span>
  `,
  styles: `
    :host {
      display: inline-flex;
    }
  `,
})
export class Badge {
  readonly tone = input<BadgeTone>('neutral');
  /** Opsiyon durumlari icin kesikli cerceve (rezervasyon grid'i kurali). */
  readonly dashed = input(false);

  protected readonly classes = computed(() => {
    const tones: Record<BadgeTone, string> = {
      neutral: 'border-rule-strong text-ink-muted bg-paper',
      navy: 'border-navy text-navy bg-navy-tint',
      copper: 'border-copper text-copper bg-copper-tint',
      brass: 'border-brass text-brass bg-brass-tint',
      danger: 'border-danger text-danger bg-danger-tint',
      success: 'border-success text-success bg-success-tint',
    };
    const border = this.dashed() ? 'border border-dashed' : 'border';
    return `${border} px-2 py-0.5 label-mono ${tones[this.tone()]}`;
  });
}
