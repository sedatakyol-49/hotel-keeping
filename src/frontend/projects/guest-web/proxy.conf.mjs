/**
 * `ng serve guest-web` icin API vekili.
 *
 * NEDEN JSON DEGIL, JS: hedef adres gelistirme sirasinda degisir. Public uclar
 * backend'de yazilirken bir **mock** sunucuya bakmak gerekir; bunun icin
 * ortam degiskeni yeterlidir ve versiyonlanan bir dosyayi degistirmek gerekmez:
 *
 *   GUEST_API_TARGET=http://localhost:5099 npm run start:guest
 *
 * Varsayilan hedef backend'in gelistirme portudur (5080).
 */
export default [
  {
    context: ['/api'],
    target: process.env.GUEST_API_TARGET ?? 'http://localhost:5080',
    secure: false,
    changeOrigin: true,
  },
];
