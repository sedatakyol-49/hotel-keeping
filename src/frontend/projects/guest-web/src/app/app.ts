import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ConsentStore } from './core/consent/consent.store';
import { TrackerService } from './core/consent/tracker.service';
import { GuestSeoService } from './core/seo/guest-seo.service';
import { BrowserStorage } from './core/storage/browser-storage';

/**
 * Kok bilesen. Yerlesim `layout/guest-shell` tarafindan saglanir; burada
 * yalnizca router cikisi ve SEO servisinin baglanmasi var.
 *
 * SEO servisi burada baglanir cunku bu bilesen SSR sirasinda da olusturulur:
 * `canonical` ve `hreflang` baglari sunucudan cikan HTML'de bulunmalidir.
 */
@Component({
  selector: 'hcg-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {
  constructor() {
    inject(GuestSeoService).connect();

    /*
     * §25 TDDDG: onay durumu YALNIZCA tarayicida okunur ve izleyici baglantisi
     * yalnizca tarayicida kurulur. Sunucu kullanicinin kararini bilemez (public
     * uclar cerez koymaz); SSR ciktisina izleyici koymak "onaysiz yukleme"
     * demek olurdu.
     */
    const storage = inject(BrowserStorage);
    if (storage.browser) {
      inject(ConsentStore).restore(storage.local);
      inject(TrackerService).connect();
    }
  }
}
