/** Misafir sitesi ortam sozlesmesi. */
export interface GuestEnvironment {
  readonly production: boolean;
  /**
   * Sitenin kanonik kok adresi — **son egik cizgi olmadan**.
   * `canonical` ve `hreflang` baglari mutlak adres ister; goreli adres veren
   * bir site ayna alan adlarinda (staging, CDN) kendi kendini kopya ilan eder.
   */
  readonly siteOrigin: string;
  /**
   * `/api/v1` ile biten taban adres. **Goreli** tutulur: tarayicida ayni
   * origin'e gider, sunucuda `apiUrlInterceptor` istegin origin'ini one ekler
   * (bkz. core/api/public-http.interceptors.ts). Gelistirmede `ng serve`
   * proxy'si `/api` isteklerini backend'e (veya bir mock'a) tasir.
   */
  readonly apiBaseUrl: string;
  /**
   * Aktif otelin public slug'i — sozlesmede yol parametresi
   * (`/api/v1/public/hotels/{hotelSlug}/...`).
   *
   * Bu tur **otel basina alan adi** dagitimini hedefler (mimari §4.1'in
   * `Hotel.PublicHost` katmani): host -> slug cevirisi dagitim yapilandirmasinda
   * yapilir, uygulama slug'i yapilandirmadan okur ve **her istekte yola koyar**.
   * Marka sitesi (cok otelli) eklendiginde bu deger bir rota parametresinden
   * beslenir; API sozlesmesi degismez.
   */
  readonly hotelSlug: string;
}
