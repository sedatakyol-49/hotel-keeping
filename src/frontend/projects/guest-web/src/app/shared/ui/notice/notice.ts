import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Cerceveli bilgi kutusu — sitedeki tek "dikkat cekme" bicimi.
 *
 * NEDEN TEK BILESEN: uyari, hukuki bildirim ve hata kutulari birbirine
 * benzemek ZORUNDA; ayri ayri yazilirsa biri gri, biri kirmizi, biri kalin
 * cerceveli olur ve sayfa "sistem ciktisi" gibi gorunur. Burada fark yalnizca
 * **sol cetvel cizgisinin rengi** ve etiket metnidir; ikon, emoji, golge yok.
 *
 * `tone` semantik degildir, gorseldir. Ekran okuyucu icin belirleyici olan
 * `role`'dur: hatalar `alert`, digerleri `note`/`region`.
 */
export type NoticeTone = 'neutral' | 'legal' | 'warning' | 'danger' | 'success';

@Component({
  selector: 'hcg-notice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="border border-rule border-l-2 bg-paper-raised px-4 py-4 sm:px-5"
      [style.border-left-color]="accent()"
      [attr.role]="role()"
      [attr.aria-live]="live()"
      [attr.data-tone]="tone()"
      data-testid="notice"
    >
      @if (label()) {
        <p class="eyebrow" data-testid="notice-label">{{ label() }}</p>
      }
      @if (heading()) {
        <p class="mt-1 font-serif text-lg" data-testid="notice-heading">{{ heading() }}</p>
      }
      <div class="mt-2 max-w-measure text-sm text-ink-muted">
        <ng-content />
      </div>
      <div class="empty:hidden">
        <ng-content select="[data-notice-actions]" />
      </div>
    </div>
  `,
})
export class Notice {
  readonly tone = input<NoticeTone>('neutral');
  readonly label = input('');
  readonly heading = input('');
  /** `alert` yalnizca kullanicinin **hemen** duymasi gereken hatalar icin. */
  readonly assertive = input(false);

  protected readonly accent = computed(() => {
    switch (this.tone()) {
      case 'legal':
        return 'var(--color-navy)';
      case 'warning':
        return 'var(--color-brass)';
      case 'danger':
        return 'var(--color-danger)';
      case 'success':
        return 'var(--color-success)';
      default:
        return 'var(--color-rule-strong)';
    }
  });

  protected readonly role = computed(() => (this.assertive() ? 'alert' : 'note'));
  protected readonly live = computed(() => (this.assertive() ? 'assertive' : null));
}
