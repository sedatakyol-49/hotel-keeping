import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import type { PublicWithdrawalRight } from '../../../core/api/public-models';
import { Notice } from '../notice/notice';

/**
 * ===========================================================================
 * §312g Abs. 2 Nr. 9 BGB — CAYMA HAKKI YOK, AMA BILDIRILMEK ZORUNDA
 * ===========================================================================
 *
 * Belirli bir tarihe bagli konaklama hizmetlerinde 14 gunluk cayma hakki
 * **yoktur** (Nr. 9: "Beherbergung zu anderen Zwecken als zu Wohnzwecken ...
 * wenn der Vertrag fur die Erbringung einen spezifischen Termin vorsieht").
 *
 * BURADAKI TEHLIKE, EKSIK BILGI DEGIL, YANLIS BILGI:
 * Genel bir "Widerrufsbelehrung" gostermek — yani var olmayan bir hakki
 * anlatmak — yaniltici olur ve tuketiciyi 14 gun icinde cayabilecegi
 * yanilgisina dusurur. Bu yuzden bilesen metnini **sunucunun bildirdigi
 * duruma** gore secer:
 *
 *   applies === false  ->  ISTISNA metni + yasal dayanak (`legalBasis`)
 *   applies === true   ->  genel cayma bildirimi (bu fazda uretilmez, ama
 *                          sozlesme alani `boolean` oldugu icin dal vardir)
 *
 * AYRICA: misafirin "hic iptal edemiyorum" sanmamasi icin metin, **sozlesmesel
 * iptal hakkinin ayri bir sey oldugunu** soyler ve iptal politikasina isaret
 * eder (mimari §9.7).
 *
 * Onay (`consents.withdrawalNoticeAcknowledged`) ve gosterilen metnin
 * **versiyonu** (`noticeVersion`) rezervasyon isteginde dondurulur.
 */
@Component({
  selector: 'hcg-withdrawal-notice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Notice],
  template: `
    <hcg-notice
      tone="legal"
      [label]="'legal.withdrawal.label' | translate"
      [heading]="headingKey() | translate"
    >
      <div [attr.data-testid]="'withdrawal-' + (right().applies ? 'applies' : 'excluded')">
        <p data-testid="withdrawal-body">{{ bodyKey() | translate }}</p>

        <p class="mt-2 text-xs text-ink-faint">
          <span data-testid="withdrawal-basis">
            {{ 'legal.withdrawal.basis' | translate: { basis: right().legalBasis } }}
          </span>
          <span class="numeric" data-testid="withdrawal-version">
            · {{ 'legal.withdrawal.version' | translate: { version: right().noticeVersion } }}
          </span>
        </p>

        @if (!right().applies) {
          <p class="mt-2" data-testid="withdrawal-cancellation-hint">
            {{ 'legal.withdrawal.cancellationHint' | translate }}
          </p>
        }
      </div>
    </hcg-notice>
  `,
})
export class WithdrawalNotice {
  readonly right = input.required<PublicWithdrawalRight>();

  /**
   * Metin anahtari sunucudan gelir (`noticeKey`). Katalogumuzda karsiligi
   * yoksa duruma gore genel anahtara duseriz — ama **asla** yanlis dala
   * gecmeyiz: `applies` neyse o gosterilir.
   */
  protected readonly bodyKey = computed(() => {
    const right = this.right();
    if (right.noticeKey.length > 0) {
      return right.noticeKey;
    }
    return right.applies
      ? 'legal.withdrawal.included.body'
      : 'legal.withdrawal.excluded.accommodation';
  });

  protected readonly headingKey = computed(() =>
    this.right().applies
      ? 'legal.withdrawal.included.title'
      : 'legal.withdrawal.excluded.title',
  );
}
