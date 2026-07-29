import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Tablo kabugu: 1px cetvel cerceve + yatay kaydirma kabi.
 *
 * Onemli: basliklar ve satirlar **ayni** kaydirma kabinda durur; sutun
 * genislikleri birebir esitlenir. Aksi halde sabit baslik ile veri satirlari
 * yatay kaydirmada birbirinden kayar (doluluk grid'i icin kritik).
 *
 * Masaustunde tablo, mobilde kart listesi kullanilir; ikisi de ayni signal
 * store'u okur — veri kopyalanmaz.
 */
@Component({
  selector: 'hc-table-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div class="border border-rule bg-paper-raised">
      <div class="overflow-x-auto overscroll-x-contain">
        <table class="w-full border-collapse text-left text-sm">
          <caption class="sr-only">
            {{
              captionKey() | translate
            }}
          </caption>
          <ng-content />
        </table>
      </div>
      <div class="border-t border-rule px-4 py-2 empty:hidden">
        <ng-content select="[slot=footer]" />
      </div>
    </div>
  `,
})
export class TableShell {
  /** Ekran okuyucular icin tablo aciklamasinin i18n anahtari. */
  readonly captionKey = input.required<string>();
}
