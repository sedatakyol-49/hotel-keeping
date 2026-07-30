import { DOCUMENT, DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { filter } from 'rxjs';

import { DEFAULT_LANGUAGE, LanguageStore, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { environment } from '../../../environments/environment';
import { withLanguage } from '../i18n/language-url';

/** Rota `data` sozlesmesi — her sayfa kendi SEO metnini anahtar olarak bildirir. */
export interface GuestRouteSeo {
  readonly titleKey?: string;
  readonly descriptionKey?: string;
  /** Kisiye ozel / sorgu bagimli sayfalar dizine eklenmez. */
  readonly noindex?: boolean;
}

/** Bu servisin ekledigi head ogeleri bu nitelikle isaretlenir (temizlik icin). */
const MANAGED = 'data-hc-seo';

@Injectable({ providedIn: 'root' })
export class GuestSeoService {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageStore);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Uygulama acilisinda bir kez baglanir. Ilk gecis de dahil her navigasyondan
   * sonra baslik, aciklama, `canonical` ve `hreflang` bagi yeniden yazilir.
   *
   * Bunun SSR sirasinda calismasi sarttir: `hreflang` istemcide eklenirse,
   * HTML'i alip JavaScript calistirmadan degerlendiren araclar uc dilin
   * birbirine baglandigini goremez.
   */
  connect(): void {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.apply());

    this.apply();
  }

  /** Test edilebilirlik icin ayri: mevcut rota durumundan head'i uretir. */
  apply(): void {
    const seo = this.collect();
    const url = stripSuffix(this.router.url);
    const language = this.language.current();

    this.title.setTitle(this.text(seo.titleKey, 'seo.defaultTitle'));

    const description = this.text(seo.descriptionKey, 'seo.defaultDescription');
    this.meta.updateTag({ name: 'description', content: description });

    if (seo.noindex === true) {
      this.meta.updateTag({ name: 'robots', content: 'noindex, follow' });
    } else {
      this.meta.removeTag("name='robots'");
    }

    this.writeLinks(url, language, description);
  }

  /** Aktif rota agacindaki en derin `data` degerlerini birlestirir. */
  private collect(): GuestRouteSeo {
    let snapshot = this.route.snapshot;
    let seo: GuestRouteSeo = {};

    for (;;) {
      seo = { ...seo, ...(snapshot.data as GuestRouteSeo) };
      const child = snapshot.firstChild;
      if (child === null) {
        return seo;
      }
      snapshot = child;
    }
  }

  private text(key: string | undefined, fallbackKey: string): string {
    const resolved: unknown = this.translate.instant(key ?? fallbackKey);
    return typeof resolved === 'string' ? resolved : (key ?? fallbackKey);
  }

  private writeLinks(url: string, language: string, description: string): void {
    const head = this.document.head;
    for (const managed of Array.from(head.querySelectorAll(`[${MANAGED}]`))) {
      managed.remove();
    }

    const absolute = (target: string) => `${environment.siteOrigin}${target}`;

    // Kanonik adres: dil on ekli, sorgu/parcasiz.
    this.appendLink({ rel: 'canonical', href: absolute(url) });

    /*
     * hreflang seti. Kurallar (Google "Localized versions"):
     *  - her dil kendisi dahil TUM alternatifleri bildirir (karsilikli olmali),
     *  - `x-default` dil pazarliginin dustugu adresi gosterir (bizde `de`).
     */
    for (const alternate of SUPPORTED_LANGUAGES) {
      this.appendLink({
        rel: 'alternate',
        hreflang: alternate,
        href: absolute(withLanguage(url, alternate)),
      });
    }
    this.appendLink({
      rel: 'alternate',
      hreflang: 'x-default',
      href: absolute(withLanguage(url, DEFAULT_LANGUAGE)),
    });

    // Paylasim onizlemesi — baslik/aciklama ile ayni kaynaktan beslenir.
    this.appendMeta('og:type', 'website');
    this.appendMeta('og:url', absolute(url));
    this.appendMeta('og:title', this.document.title);
    this.appendMeta('og:description', description);
    this.appendMeta('og:locale', language);
  }

  private appendLink(attributes: Record<string, string>): void {
    const link = this.document.createElement('link');
    for (const [name, value] of Object.entries(attributes)) {
      link.setAttribute(name, value);
    }
    link.setAttribute(MANAGED, '');
    this.document.head.appendChild(link);
  }

  private appendMeta(property: string, content: string): void {
    const tag = this.document.createElement('meta');
    tag.setAttribute('property', property);
    tag.setAttribute('content', content);
    tag.setAttribute(MANAGED, '');
    this.document.head.appendChild(tag);
  }
}

function stripSuffix(url: string): string {
  const index = [url.indexOf('?'), url.indexOf('#')].filter((value) => value !== -1);
  return index.length > 0 ? url.slice(0, Math.min(...index)) : url;
}
