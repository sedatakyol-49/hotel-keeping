import { Injectable, computed, signal } from '@angular/core';

/**
 * ===========================================================================
 * §25 TDDDG — CEREZ / IZLEYICI ONAYI
 * ===========================================================================
 *
 * Kural (mimari §9.5): **onaysiz hicbir zorunlu-olmayan depolama veya erisim
 * yapilmaz** — analitik dahil. Ucuncu taraf script'ler onay gelene kadar
 * DOM'a **hic eklenmez** (gizlemek yetmez, §25 "Speichern von Informationen
 * ... oder der Zugriff" ifadesi yuklemenin kendisini kapsar).
 *
 * ZORUNLU DEPOLAMA ISTISNASI (§25 Abs. 2 Nr. 2) — onaysiz kullanilabilenler:
 *   - `holdToken` (sessionStorage): kullanicinin acikca talep ettigi hizmetin
 *     (rezervasyonun) surdurulebilmesi icin gereklidir,
 *   - onay kararinin kendisi (localStorage): karari saklamamak, her sayfada
 *     yeniden sormak demektir; bu da onayin ifadesini imkansiz kilar.
 * Baska hicbir sey bu istisnaya girmez.
 *
 * ARAYUZ KURALLARI (Planet49 / EuGH C-673/17 ve DSK yonergesi):
 *   - **On isaretli kutu YOK.** Baslangic durumu `unknown`; ne kabul ne ret.
 *   - **"Reddet" en az "Kabul et" kadar kolay**: ayni seviyede, ayni olcude,
 *     ayni tiklama sayisi. Renk/agirlik farki da kurulmaz (bkz. bilesen).
 *   - Karar geri alinabilir: alt bilgideki "Cerez ayarlari" bandi geri getirir.
 *
 * Bu store yalnizca DURUMU tutar; script yukleme `TrackerService`'in isidir.
 */

export type ConsentDecision = 'unknown' | 'granted' | 'denied';

/** Karar formati degisirse eski kararlar gecersiz olur (yeniden sorulur). */
const STORAGE_KEY = 'hc.tdddg.consent.v1';

interface StoredConsent {
  readonly decision: 'granted' | 'denied';
  readonly decidedAt: string;
  readonly version: 1;
}

@Injectable({ providedIn: 'root' })
export class ConsentStore {
  private readonly _decision = signal<ConsentDecision>('unknown');
  /** Kullanici karari geri almak isterse bant yeniden acilir. */
  private readonly _reopened = signal(false);

  readonly decision = this._decision.asReadonly();

  /** Zorunlu-olmayan izleyiciler yalnizca bu `true` iken calisabilir. */
  readonly analyticsAllowed = computed(() => this._decision() === 'granted');

  /** Onay bandi gorunur mu? (Karar verilmemis veya kullanici yeniden acmis.) */
  readonly bannerVisible = computed(() => this._decision() === 'unknown' || this._reopened());

  /**
   * Kaydedilmis karari okur. SSR'da cagrilmaz: sunucunun kullanicinin kararini
   * bilmesi mumkun degildir (cerez konmuyor) ve bir "varsayilan" render etmek
   * ya bandi bosuna gosterir ya da izleyiciyi onaysiz calistirir.
   */
  restore(storage: Storage | null): void {
    if (storage === null) {
      return;
    }
    try {
      const raw = storage.getItem(STORAGE_KEY);
      if (raw === null) {
        return;
      }
      const parsed: unknown = JSON.parse(raw);
      if (
        parsed !== null &&
        typeof parsed === 'object' &&
        (parsed as StoredConsent).version === 1 &&
        ((parsed as StoredConsent).decision === 'granted' ||
          (parsed as StoredConsent).decision === 'denied')
      ) {
        this._decision.set((parsed as StoredConsent).decision);
      }
    } catch {
      /* Bozuk/erisilemez depo: karar verilmemis sayilir, izleyici calismaz. */
    }
  }

  accept(storage: Storage | null): void {
    this.decide('granted', storage);
  }

  decline(storage: Storage | null): void {
    this.decide('denied', storage);
  }

  /** Alt bilgideki "Cerez ayarlari" — karari degistirmeyi mumkun kilar. */
  reopen(): void {
    this._reopened.set(true);
  }

  private decide(decision: 'granted' | 'denied', storage: Storage | null): void {
    this._decision.set(decision);
    this._reopened.set(false);

    if (storage === null) {
      return;
    }
    const record: StoredConsent = {
      decision,
      decidedAt: new Date().toISOString(),
      version: 1,
    };
    try {
      storage.setItem(STORAGE_KEY, JSON.stringify(record));
    } catch {
      /* Depolama reddedildiyse karar yalnizca bu oturum icin gecerlidir. */
    }
  }
}
