import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { GuestSeoService } from './core/seo/guest-seo.service';

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
  }
}
