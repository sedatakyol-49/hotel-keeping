import { RenderMode, type ServerRoute } from '@angular/ssr';

import { SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { LEGAL_DOCUMENTS } from './features/legal/legal-documents';

/**
 * ===========================================================================
 * RENDER MODU KARARI — neden "hepsi prerender" veya "hepsi SSR" degil
 * ===========================================================================
 *
 * TEK OLCUT: **sayfanin icerigi derleme aninda dogru olabilir mi?**
 *
 *  - HUKUKI SAYFALAR (Impressum / Datenschutz / AGB): icerik versiyonlu bir
 *    belgedir, fiyat iddiasi tasimaz ve nadiren degisir. Derleme aninda alinan
 *    metin **yayimlanmis** metindir. -> PRERENDER (SSG). §5 DDG kunyenin
 *    "unmittelbar erreichbar" olmasini ister; dosya olarak servis edilen bir
 *    sayfa bu kosulu en gucu bicimde saglar. Icerik derleme oncesi alinmis
 *    anlik goruntuden gelir (core/legal/legal-snapshot.ts).
 *
 *  - ANA SAYFA: katalog kartlari **"ab" FIYATI** tasir ve oda tipi listesi
 *    veritabanindan gelir. -> SERVER (SSR).
 *    <b>Bu bir duzeltmedir:</b> ana sayfa once "herkes icin ayni, nadiren
 *    degisir" gerekcesiyle prerender edilmisti. O gerekce yanlisti ve kendi
 *    kuralimizla celisiyordu: oda tipi detayini SSR'a koyarken gerekcemiz
 *    "onceden uretilmis sayfa gecen haftanin fiyatini gosterir" idi — ayni
 *    cumle ana sayfa icin de gecerli. Depoda bayatlayan bir "ab 139 €",
 *    PAngV/UWG acisindan **yanlis bir fiyat iddiasidir**; hukuki metnin
 *    bayatlamasindan kategorik olarak farklidir (biri eski ama yayimlanmis bir
 *    belgedir, digeri bugun gecerli olmayan bir fiyattir). Ayrica prerender
 *    sirasinda API olmadigi icin sayfa katalogsuz ureiliyordu: arama motoru
 *    ana sayfada ne oda adi ne fiyat goruyordu — yani prerender'in SEO gerekcesi
 *    de fiilen calismiyordu.
 *
 *  - ODA TIPI DETAYI: SEO'nun asil hedefi, ama fiyat ve musaitlik CANLI veridir.
 *    -> SERVER (SSR). Ana sayfayla ayni gerekce.
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
 * Ozet: **fiyat tasiyan hicbir sayfa prerender edilmez.** Prerender edilen tek
 * sey, derleme aninda dogru olabilen icerik: versiyonlu hukuki belgeler.
 * Bedeli, ana sayfanin CDN'den dosya olarak degil SSR sunucusundan gelmesidir
 * (TTFB); kazanci, fiyatin her zaman canli olmasidir.
 *
 * Bu kural derleme adiminda ZORLANIR: `scripts/verify-build-output.mjs`
 * prerender ciktisinda hukuki sayfalar disinda bir sayfa gorurse derlemeyi kirar.
 */

/** Prerender edilecek dil parametreleri: `/de`, `/en`, `/tr`. */
const languageParams = (): Promise<Record<string, string>[]> =>
  Promise.resolve(SUPPORTED_LANGUAGES.map((lang) => ({ lang })));

export const serverRoutes: ServerRoute[] = [
  // Dil pazarligi istegin `Accept-Language` basligini okur -> onceden uretilemez.
  { path: '', renderMode: RenderMode.Server },

  // Ana sayfa: katalog ve "ab" fiyati CANLI veridir (yukaridaki gerekce).
  { path: ':lang', renderMode: RenderMode.Server },

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
