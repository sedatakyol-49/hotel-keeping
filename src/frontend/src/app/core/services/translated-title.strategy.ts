import { Injectable, Injector, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { TitleStrategy, type RouterStateSnapshot } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

const APP_NAME = 'HotelCore';

/**
 * Sekme basligini rota `data.titleKey` degerinden ceviri ile uretir.
 * Dil degistiginde baslik da guncellenir (a11y ve tarayici gecmisi icin).
 *
 * Tasarim notu: `TitleStrategy`, `Router` kurulurken (NavigationTransitions
 * icinde) cozulur. Bu nedenle bagimliliklar **tembel** alinir; aksi halde
 * `Router -> TitleStrategy -> TranslateService -> HttpClient` zinciri acilis
 * sirasinda NG0200 (dairesel bagimlilik) uretir.
 *
 * Not: `providedIn: 'root'` kullanilmaz; `TitleStrategy` token'ina `useClass`
 * ile baglanir (bkz. `app.config.ts`).
 */
@Injectable()
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly injector = inject(Injector);

  private lastTitleKey: string | null = null;
  private languageSubscribed = false;

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const route = this.deepestRoute(snapshot);
    this.lastTitleKey = (route.data['titleKey'] as string | undefined) ?? null;
    this.ensureLanguageSubscription();
    this.apply(this.lastTitleKey);
  }

  /** Ilk gezinmede bir kez baglanir; kok kapsamli oldugu icin omur boyu yasar. */
  private ensureLanguageSubscription(): void {
    if (this.languageSubscribed) {
      return;
    }
    this.languageSubscribed = true;
    this.injector.get(TranslateService).onLangChange.subscribe(() => this.apply(this.lastTitleKey));
  }

  private apply(titleKey: string | null): void {
    const title = this.injector.get(Title);
    if (!titleKey) {
      title.setTitle(APP_NAME);
      return;
    }
    const translated = this.injector.get(TranslateService).instant(titleKey) as string;
    title.setTitle(`${translated} · ${APP_NAME}`);
  }

  private deepestRoute(snapshot: RouterStateSnapshot) {
    let route = snapshot.root;
    while (route.firstChild) {
      route = route.firstChild;
    }
    return route;
  }
}
