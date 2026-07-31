/**
 * `ng serve guest-web` icin API vekili.
 *
 * NEDEN JSON DEGIL JS: hedef adres ortama gore degisir ve versiyonlanan bir
 * dosyayi duzenlemek gerekmemelidir. Ortam degiskeni yeterlidir:
 *
 *   GUEST_API_TARGET=http://localhost:5081 npm run start:guest
 *
 * Varsayilan hedef backend'in gelistirme portudur (5080). Misafir sitesi 4300'de
 * kosar; 4200 YONETIM panelidir. Ayni degisken hukuki anlik goruntuyu ureten
 * betikte de kullanilir (`npm run legal:snapshot`), boylece "API nerede" sorusunun
 * tek bir cevabi olur.
 *
 * Vekil hem tarayici hem SSR isteklerini tasir: `apiUrlInterceptor` sunucuda
 * goreli adresin onune istegin kendi origin'ini ekler, o da dev-server'dir.
 */
export default [
  {
    context: ['/api'],
    target: process.env.GUEST_API_TARGET ?? 'http://localhost:5080',
    secure: false,
    changeOrigin: true,
  },
];
