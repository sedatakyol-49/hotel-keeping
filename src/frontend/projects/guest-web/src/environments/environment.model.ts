/** Misafir sitesi ortam sozlesmesi. */
export interface GuestEnvironment {
  readonly production: boolean;
  /**
   * Sitenin kanonik kok adresi — **son egik cizgi olmadan**.
   * `canonical` ve `hreflang` baglari mutlak adres ister; goreli adres veren
   * bir site ayna alan adlarinda (staging, CDN) kendi kendini kopya ilan eder.
   */
  readonly siteOrigin: string;
  /** `/api/v1` ile biten mutlak veya goreli taban adres (sonraki turda kullanilacak). */
  readonly apiBaseUrl: string;
}
