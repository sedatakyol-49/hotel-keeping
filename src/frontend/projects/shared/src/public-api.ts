/*
 * @hotelcore/shared — iki uygulamanin ortak katmani.
 *
 * KIMLER KULLANIR
 *   - hotelcore-web  : giris arkasindaki yonetim paneli (src/)
 *   - guest-web      : misafire acik rezervasyon sitesi (projects/guest-web/)
 *
 * SINIR — buraya NE girer:
 *   1) Marka isareti (`BrandMark`). Iki uygulamada da ayni monogram cizilmelidir;
 *      kopyalanirsa iki marka olur.
 *   2) Dil sozlesmesi (`AppLanguage`, desteklenen diller, locale eslemesi) ve
 *      dilin **durumu** (`LanguageStore`). Sozlesme ortak olmazsa panel `tr`
 *      derken site `tr-TR` der ve API `Accept-Language` basliklari ayrisir.
 *   3) Tasarim tokenlari (`../styles/theme.css`) — tek renk/tipografi sistemi.
 *   4) Bicimlendirme (`formatting/format.ts`): para/tarih/yuzde/geri sayim.
 *      Ayni tutar iki uygulamada ayni okunmali; `de-DE` biciminin iki kopyasi
 *      olursa bir gun biri virgul, digeri nokta gosterir.
 *   5) (Sonraki tur) OpenAPI'dan uretilen API tipleri.
 *
 * SINIR — buraya NE girmez:
 *   - **Yan etki politikasi.** Dilin nereden okunacagi uygulamaya gore degisir:
 *     panelde `localStorage`, misafir sitesinde URL on eki (SEO). Bu yuzden
 *     `LanguageStore` (durum) paylasilir, `LanguageService` (politika) paylasilmaz;
 *     her uygulama kendi servisini yazar.
 *   - Yerlesim/kabuk bilesenleri. Panelin yogun defter dili ile misafir tarafinin
 *     fotograf/bosluk dili farklidir; ortak bir "header" ikisine de kotu uyar.
 *   - Ozellik (feature) kodu, guard, interceptor: uygulamaya ozgudur.
 *
 * TEKNIK KARAR — bu bir ng-packagr kutuphanesi DEGILDIR.
 * Kaynak, `tsconfig.json` -> `paths` uzerinden dogrudan derlenir. Paket npm'e
 * yayinlanmadigi icin ayri bir derleme hattinin (partial compilation, dist
 * baglama, watch modunda ikinci build) maliyeti karsiligi yoktur; kaynak
 * paylasimi her iki uygulamada da tam tip guvenligi ve tek adimda build verir.
 */

export * from './formatting/format';
export * from './i18n/language.model';
export * from './i18n/language.store';
export * from './ui/brand-mark/brand-mark';
