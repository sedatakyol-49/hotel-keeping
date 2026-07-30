import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/**
 * Aktif adresin signal karsiligi.
 *
 * Dil secici, kullaniciyi **bulundugu sayfada** tutarak dil degistirmek zorunda
 * (`/en/legal/imprint` -> `/tr/legal/imprint`); bunun icin her render'da guncel
 * URL gerekir. `Router.url` bir signal olmadigi icin burada tek bir yerde
 * signal'e cevrilir — her bilesenin ayri ayri router olayina abone olmasi
 * yerine.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUrlStore {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly _url = signal(this.router.url);

  readonly url = this._url.asReadonly();

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => this._url.set(event.urlAfterRedirects));
  }
}
