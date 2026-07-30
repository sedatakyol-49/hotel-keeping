/**
 * Hukuki belgelerin **tek kaynak listesi**.
 *
 * Hem rota tablosu (`legal.routes.ts`) hem alt bilgi (`guest-footer.ts`) bu
 * diziden beslenir. Sebep basit: bir belge eklenip alt bilgiye konmayi
 * unutulursa, Almanya'da bu bir uyari mektubu (Abmahnung) konusudur — liste
 * tek olursa unutulamaz. Bir birim test de rota sayisi ile baglanti sayisinin
 * esitligini dogrular.
 *
 * Slug'lar dilden bagimsizdir (`imprint` / `privacy` / `terms`), gorunur ad
 * cevrilir. Yerellestirilmis slug (`/de/impressum`, `/en/imprint`) kullanilmadi:
 * o durumda her dil icin ayri bir rota tablosu ve `hreflang` icin ayrica bir
 * yol eslesme tablosu gerekirdi; kazanci (anahtar kelimeli URL) ise cevrilmis
 * baslik ve icerik yaninda kucuktur.
 */
export interface LegalDocument {
  /** URL segmenti — `/{lang}/legal/{slug}` */
  readonly slug: 'imprint' | 'privacy' | 'terms';
  /** Gorunur ad anahtari (DE: Impressum / Datenschutz / AGB). */
  readonly labelKey: string;
  /** Sayfadaki `<h1>` icin anahtar. */
  readonly titleKey: string;
  /**
   * Tarayici sekmesi / arama sonucu basligi icin anahtar.
   * `<h1>`den ayri tutulur cunku `<title>` marka adini da tasimali
   * ("Impressum — HotelCore"), sayfa basligi ise tasimamalidir (tekrar olurdu).
   */
  readonly metaTitleKey: string;
}

export const LEGAL_DOCUMENTS: readonly LegalDocument[] = [
  {
    slug: 'imprint',
    labelKey: 'legal.imprint.label',
    titleKey: 'legal.imprint.title',
    metaTitleKey: 'legal.imprint.meta.title',
  },
  {
    slug: 'privacy',
    labelKey: 'legal.privacy.label',
    titleKey: 'legal.privacy.title',
    metaTitleKey: 'legal.privacy.meta.title',
  },
  {
    slug: 'terms',
    labelKey: 'legal.terms.label',
    titleKey: 'legal.terms.title',
    metaTitleKey: 'legal.terms.meta.title',
  },
];
