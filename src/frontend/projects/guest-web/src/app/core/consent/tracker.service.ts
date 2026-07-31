import { DOCUMENT, Injectable, Injector, effect, inject, signal } from '@angular/core';

import { ConsentStore } from './consent.store';

/**
 * Zorunlu-olmayan izleyicilerin **tek** yukleme noktasi.
 *
 * §25 TDDDG: onay gelene kadar script DOM'a **eklenmez**. Bu yuzden yukleme
 * tek bir yerde toplanir; bir gelistiricinin `index.html`'e etiket yapistirmasi
 * yerine buraya kayit yapmasi gerekir ve buradan gecen her sey onaya baglidir.
 *
 * Bu fazda gercek bir saglayici yok: `TRACKERS` bos. Bos olmasi bileseni
 * gereksiz kilmaz — kural, saglayici eklendiginde de gecerli kalmali ve bir
 * test bunu korumali (bkz. tracker.service.spec.ts).
 *
 * Onay geri alinirsa (banttan "reddet") eklenen etiketler **kaldirilir**.
 * Not: bir kez calisan bir script'in etkisi geri alinamaz; bu yuzden asil
 * koruma "hic eklememektir", kaldirma yalnizca ikinci savunma hattidir.
 */
interface TrackerDefinition {
  readonly id: string;
  readonly src: string;
  readonly attributes?: Readonly<Record<string, string>>;
}

const TRACKERS: readonly TrackerDefinition[] = [];

@Injectable({ providedIn: 'root' })
export class TrackerService {
  private readonly document = inject(DOCUMENT);
  private readonly consent = inject(ConsentStore);
  private readonly injector = inject(Injector);
  private readonly connected = signal(false);

  /** Etiketler bu nitelikle isaretlenir; temizlik tek sorguyla yapilir. */
  private static readonly MARKER = 'data-hc-tracker';

  /**
   * Yalnizca **tarayicida** baglanir. Sunucuda calistirilmasi anlamsizdir:
   * onay kararini bilemez ve SSR ciktisina script koymak "onaysiz yukleme"
   * demek olurdu.
   */
  connect(): void {
    if (this.connected()) {
      return;
    }
    this.connected.set(true);

    effect(
      () => {
        if (this.consent.analyticsAllowed()) {
          this.mount();
        } else {
          this.unmount();
        }
      },
      { injector: this.injector },
    );
  }

  /** Test edilebilirlik icin acik: onay verilmeden CAGRILMAZ. */
  private mount(): void {
    for (const tracker of TRACKERS) {
      if (this.document.getElementById(tracker.id) !== null) {
        continue;
      }
      const script = this.document.createElement('script');
      script.id = tracker.id;
      script.src = tracker.src;
      script.async = true;
      script.setAttribute(TrackerService.MARKER, '');
      for (const [name, value] of Object.entries(tracker.attributes ?? {})) {
        script.setAttribute(name, value);
      }
      this.document.head.appendChild(script);
    }
  }

  private unmount(): void {
    for (const element of Array.from(
      this.document.querySelectorAll(`[${TrackerService.MARKER}]`),
    )) {
      element.remove();
    }
  }

  /** Kayitli izleyici sayisi — testler ve teshis icin. */
  static get trackerCount(): number {
    return TRACKERS.length;
  }
}
