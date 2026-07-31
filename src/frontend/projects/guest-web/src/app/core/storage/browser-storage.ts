import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/**
 * Tarayici deposuna **tek** erisim noktasi.
 *
 * Iki sebeple servis:
 *  1) SSR'da `localStorage` yoktur; her kullanicinin platform kontrolu yazmasi
 *     yerine burada bir kez yapilir ve sunucuda `null` doner.
 *  2) §25 TDDDG: hangi anahtarin **zorunlu** oldugu belgelenmelidir. Zorunlu
 *     anahtarlar burada sayilir; listeye girmeyen bir sey onaya tabidir ve
 *     buradan yazilmaz.
 *
 * ZORUNLU (onaysiz kullanilabilir — §25 Abs. 2 Nr. 2):
 *   - `hc.hold` (sessionStorage): aktif hold token'i. Kullanicinin acikca
 *     talep ettigi rezervasyon hizmetinin surdurulmesi icin gereklidir.
 *   - `hc.tdddg.consent.v1` (localStorage): onay kararinin kendisi.
 * Gizlilik metninde bu iki kalem **yazili** olarak durur.
 */
@Injectable({ providedIn: 'root' })
export class BrowserStorage {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** Kalici depo (onay karari). Sunucuda `null`. */
  get local(): Storage | null {
    return this.isBrowser ? safeStorage(() => globalThis.localStorage) : null;
  }

  /** Oturum deposu (hold token'i). Sekme kapaninca silinir — istenen davranis. */
  get session(): Storage | null {
    return this.isBrowser ? safeStorage(() => globalThis.sessionStorage) : null;
  }

  get browser(): boolean {
    return this.isBrowser;
  }
}

/** Gizli sekme/kisitli mod: erisim `SecurityError` atabilir. */
function safeStorage(read: () => Storage): Storage | null {
  try {
    const storage = read();
    const probe = '__hc_probe__';
    storage.setItem(probe, '1');
    storage.removeItem(probe);
    return storage;
  } catch {
    return null;
  }
}
