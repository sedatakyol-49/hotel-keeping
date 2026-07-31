import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * ===========================================================================
 * SABIT UST BILGININ **CSS SOZLESMESI**
 * ===========================================================================
 *
 * Bilesen testleri sinif adinin sablonda oldugunu dogrular; bu dosya o sinifin
 * ARKASINDAKI kurallari dogrular. jsdom global stil sayfasini yuklemedigi icin
 * `getComputedStyle` burada bir sey soylemez — kaynak metin okunur.
 *
 * Amac "CSS'i tekrar yazmak" degil, sessizce kaybolduklarinda urunun bozuldugu
 * DORT karari kilitlemektir:
 *   1) ust bilgi yapisir,
 *   2) yapiskan yan sutunlarin ustune bindirilmesin diye z-index tasir,
 *   3) capa/odak kaydirmasi cubugun altina dusmesin diye scroll-padding var,
 *   4) atlama baglantisi sabit cubukla birlikte gorunur kalsin diye `fixed`.
 */
/*
 * Testler paketlenerek calistigi icin `import.meta.url` bir dosya adresi
 * degildir; stil sayfasi calisma dizininden cozulur (ng test her zaman
 * `src/frontend` icinden kosar). Ikinci aday, betigin depo kokunden
 * calistirildigi durum icindir.
 */
const CANDIDATES = [
  'projects/guest-web/src/styles.css',
  'src/frontend/projects/guest-web/src/styles.css',
].map((relative) => resolve(process.cwd(), relative));

const STYLESHEET = CANDIDATES.find((path) => existsSync(path));

const STYLES = readFileSync(STYLESHEET ?? CANDIDATES[0], 'utf8');

/** `.secici { ... }` blogunun govdesini dondurur. */
function ruleBody(selector: string): string {
  const index = STYLES.indexOf(`${selector} {`);
  expect(index, `${selector} kurali yok`).toBeGreaterThan(-1);
  return STYLES.slice(index, STYLES.indexOf('}', index));
}

describe('Sabit ust bilgi — CSS sozlesmesi', () => {
  it('ust bilgi yapisir (sticky, top: 0)', () => {
    const body = ruleBody('.hcg-header');
    expect(body).toContain('position: sticky');
    expect(body).toContain('top: 0');
  });

  it('opak zemin tasir (altindan gecen icerik okunmasin)', () => {
    expect(ruleBody('.hcg-header')).toContain('background-color: var(--color-canvas)');
  });

  it('z-index tasir — yapiskan yan sutunlar ustune BINEMEZ', () => {
    /*
     * Rezervasyon ve oda detayi sayfalarindaki `lg:sticky` yan sutunlar da
     * konumlandirilmis ogelerdir ve DOM'da ust bilgiden SONRA gelir. Ikisi de
     * `z-index: auto` olsaydi boyama sirasi gec geleni ustte birakirdi.
     */
    const match = /\.hcg-header \{[^}]*z-index:\s*(\d+)/.exec(STYLES);
    expect(match, 'z-index yok').not.toBeNull();
    expect(Number(match?.[1])).toBeGreaterThan(0);
  });

  it('acik menu, onay bandinin ALTINDA kalmaz', () => {
    /*
     * Olculdu: 375x667'de bant (z-50) menu panelinin son satirini (TR)
     * ortuyordu. Ust bilgi bandin uzerinde olmali, aksi halde ilk ziyarette
     * dil degistirmek isteyen kullanici bir secenegi hic goremez.
     */
    const header = Number(/\.hcg-header \{[^}]*z-index:\s*(\d+)/.exec(STYLES)?.[1]);
    expect(header).toBeGreaterThan(50);
  });

  it('capa ve odak kaydirmasi cubugun ALTINA dusmez', () => {
    expect(STYLES).toContain('scroll-padding-top: calc(var(--spacing-header) + 1rem)');
    expect(STYLES).toContain('scroll-padding-top: calc(var(--spacing-header-wide) + 1rem)');
  });

  it('atlama baglantisi sabit cubukla birlikte gorunur kalir (fixed)', () => {
    expect(ruleBody('.hc-skip-link')).toContain('position: fixed');
  });

  it('menu paneli kendi kaydirma kabidir ve konumlandirma baglami tasir', () => {
    /*
     * `overflow-y: auto` bir kaydirma kabi yaratir. Panelde yasanan hatanin
     * ayni sinifi: konumlandirma baglami olmayan bir kapta `sr-only`
     * (position: absolute) ogeler kaptan kacip BELGEYI buyutur.
     */
    const body = ruleBody('.hcg-header-menu');
    expect(body).toContain('position: relative');
    expect(body).toContain('overflow-y: auto');
    expect(body).toContain('max-height: calc(100dvh - var(--spacing-header))');
  });

  it('yukseklikler token olarak tanimlidir (sablonda sabit sayi yok)', () => {
    expect(STYLES).toContain('--spacing-header: 3.5rem');
    expect(STYLES).toContain('--spacing-header-wide: 4rem');
    expect(STYLES).toContain('--spacing-below-header: 5.5rem');
  });

  it('yasakli gorsel araclar geri gelmemis (yuvarlak kose / golge)', () => {
    const header = ruleBody('.hcg-header') + ruleBody('.hcg-header-menu');
    expect(header).not.toMatch(/border-radius|box-shadow/);
  });
});
