import { RenderMode, type ServerRoute } from '@angular/ssr';

import { SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { LEGAL_DOCUMENTS } from './features/legal/legal-documents';

/**
 * ===========================================================================
 * RENDER MODU KARARI — neden "hepsi prerender" veya "hepsi SSR" degil
 * ===========================================================================
 *
 * Misafir sitesinin icerigi tek tip degildir; render modu da tek tip olmamali:
 *
 *  - ANA SAYFA ve HUKUKI SAYFALAR: herkes icin ayni, nadiren degisir, SEO
 *    acisindan kritik. -> PRERENDER (SSG). Derleme aninda uretilir, istek
 *    aninda CDN'den dosya olarak doner: en hizli TTFB, sifir sunucu maliyeti.
 *    Dil basina bir dosya (`/de`, `/en`, `/tr`).
 *
 *  - ODA TIPI DETAYI: SEO'nun asil hedefi, ama fiyat ve musaitlik CANLI veridir.
 *    Onceden uretilmis bir sayfa bir hafta once gecerli olan fiyati gosterirdi;
 *    fiyat yanlissa sayfa yalan soyler. Ayrica oda tipi listesi veritabanindan
 *    gelir, derleme aninda bilinmez (bu turda API de yok). -> SERVER (SSR).
 *
 *  - ARAMA SONUCLARI: sorgu bagimli; onceden uretilebilecek sonlu bir kume
 *    degil. Yine de sunucuda render edilir: ilk boyama hizli olsun ve JavaScript
 *    beklenmeden icerik gorunsun. Dizine eklenmez (rota `data.noindex`).
 *    -> SERVER.
 *
 *  - REZERVASYON ve ONAY: misafirin adi, e-postasi, rezervasyon numarasi.
 *    SEO degeri sifir, sunucuda render etmenin tek etkisi kisisel veriyi
 *    sunucu tarafina (ve olasi ara onbelleklere) tasimak olurdu. -> CLIENT.
 *
 * Ozet: statik olan onceden uretilir, canli olan istek aninda uretilir, ozel
 * olan hic sunucuya ugramaz.
 */

/** Prerender edilecek dil parametreleri: `/de`, `/en`, `/tr`. */
const languageParams = (): Promise<Record<string, string>[]> =>
  Promise.resolve(SUPPORTED_LANGUAGES.map((lang) => ({ lang })));

export const serverRoutes: ServerRoute[] = [
  // Dil pazarligi istegin `Accept-Language` basligini okur -> onceden uretilemez.
  { path: '', renderMode: RenderMode.Server },

  {
    path: ':lang',
    renderMode: RenderMode.Prerender,
    getPrerenderParams: languageParams,
  },

  ...LEGAL_DOCUMENTS.map((document): ServerRoute => ({
    path: `:lang/legal/${document.slug}`,
    renderMode: RenderMode.Prerender,
    getPrerenderParams: languageParams,
  })),

  {
    path: ':lang/rooms/:slug',
    renderMode: RenderMode.Server,
  },
  {
    path: ':lang/search',
    renderMode: RenderMode.Server,
    // Baslik de gonderilir: HTML'i okumayan araclar (ornegin dosya indirenler)
    // ve arama motorlarinin ham HTTP katmani icin ayni sinyal.
    headers: { 'X-Robots-Tag': 'noindex, follow' },
  },
  {
    path: ':lang/booking',
    renderMode: RenderMode.Client,
    headers: { 'X-Robots-Tag': 'noindex, nofollow' },
  },
  {
    path: ':lang/confirmation/:token',
    renderMode: RenderMode.Client,
    headers: { 'X-Robots-Tag': 'noindex, nofollow' },
  },
  /*
   * Sorgulama ve iptal: rezervasyon sahibinin adi, e-postasi ve tutarlari
   * gorunur. Onay ekraniyla ayni gerekce — sunucuya hic ugramaz.
   * Arama formu (`/manage`) da istemcidir: girilen e-posta sunucu loglarina
   * dusmemeli.
   */
  {
    path: ':lang/manage',
    renderMode: RenderMode.Client,
    headers: { 'X-Robots-Tag': 'noindex, nofollow' },
  },
  {
    path: ':lang/manage/:token',
    renderMode: RenderMode.Client,
    headers: { 'X-Robots-Tag': 'noindex, nofollow' },
  },

  /*
   * Dil on ekinin ICINDEKI bilinmeyen adres = gercek 404.
   * Durum kodu acikca 404'tur: yerellestirilmis hata sayfasini HTTP 200 ile
   * dondurmek "soft 404"tur ve arama motoru bu sayfalari icerik sanip
   * indeksler.
   */
  { path: ':lang/**', renderMode: RenderMode.Server, status: 404 },

  // Dil on eksiz her sey: dil pazarligi + yonlendirme (302).
  { path: '**', renderMode: RenderMode.Server },
];
