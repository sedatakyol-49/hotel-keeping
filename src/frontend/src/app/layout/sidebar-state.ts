import { Injectable, computed, signal } from '@angular/core';

const COLLAPSED_KEY = 'hotelcore.sidebar.collapsed';
const EXPANDED_KEY = 'hotelcore.sidebar.expandedGroups';

/**
 * Kenar cubugu gorunum durumu — iki bagimsiz eksen:
 *
 * 1. **Daraltma (rail):** masaustunde kenar cubugu tam genislik ile ince "rail"
 *    arasinda gecis yapar. Rail modunda ana kalemler yalnizca kisa tipografik
 *    gosterimle durur, alt menu ucan panel olarak acilir. Mobilde anlamsizdir
 *    (orada cekmece kullanilir), bu yuzden dugme yalnizca `lg` ve uzerinde
 *    gosterilir.
 * 2. **Alt menu aciklik durumu:** hangi ana kalemlerin alt menusu acik. Aktif
 *    rotayi iceren kalem `ensureExpanded` ile kendiliginden acilir.
 *
 * Iki durum da `localStorage`'da saklanir; sayfa yenilenince kullanicinin
 * tercihi korunur. Erisim savunmaci sekilde sarilir (private mode / kisitli
 * tarayici) — depolama yoksa uygulama varsayilanlarla calisir.
 */
@Injectable({ providedIn: 'root' })
export class SidebarState {
  private readonly _collapsed = signal(readCollapsed());
  private readonly _expanded = signal<ReadonlySet<string>>(readExpanded());

  /** Masaustunde rail moduna alinmis mi. */
  readonly collapsed = this._collapsed.asReadonly();

  /** Acik alt menulerin anahtar kumesi (salt okunur gorunum). */
  readonly expandedGroups = computed<ReadonlySet<string>>(() => this._expanded());

  toggleCollapsed(): void {
    this._collapsed.update((collapsed) => {
      const next = !collapsed;
      persist(COLLAPSED_KEY, next ? '1' : '0');
      return next;
    });
  }

  isExpanded(groupKey: string): boolean {
    return this._expanded().has(groupKey);
  }

  toggleGroup(groupKey: string): void {
    this._expanded.update((current) => {
      const next = new Set(current);
      if (!next.delete(groupKey)) {
        next.add(groupKey);
      }
      persistExpanded(next);
      return next;
    });
  }

  /**
   * Aktif rotayi iceren ana kalemi acar. Kullanicinin elle kapattigi baska
   * kalemlere dokunmaz — yani "kendiliginden ac", "digerlerini kapat" degil;
   * boylece birden fazla bolumu acik tutan kullanici alisknligini bozmaz.
   */
  ensureExpanded(groupKey: string): void {
    if (this._expanded().has(groupKey)) {
      return;
    }

    this._expanded.update((current) => {
      const next = new Set(current).add(groupKey);
      persistExpanded(next);
      return next;
    });
  }
}

function readCollapsed(): boolean {
  try {
    return globalThis.localStorage?.getItem(COLLAPSED_KEY) === '1';
  } catch {
    return false;
  }
}

function readExpanded(): ReadonlySet<string> {
  try {
    const raw = globalThis.localStorage?.getItem(EXPANDED_KEY);
    if (raw === null || raw === undefined) {
      return new Set();
    }

    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed)
      ? new Set(parsed.filter((entry): entry is string => typeof entry === 'string'))
      : new Set();
  } catch {
    // Bozuk/erisilemez deger sessizce yok sayilir: gezinme calismaya devam eder.
    return new Set();
  }
}

function persistExpanded(groups: ReadonlySet<string>): void {
  persist(EXPANDED_KEY, JSON.stringify([...groups]));
}

function persist(key: string, value: string): void {
  try {
    globalThis.localStorage?.setItem(key, value);
  } catch {
    // Depolama yoksa tercih yalnizca bu oturumda gecerli olur.
  }
}
