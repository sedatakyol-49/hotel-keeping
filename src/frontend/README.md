# HotelCore — Frontend workspace

Angular 22 (standalone + Signals) · Tailwind CSS v4 (CSS-first) · ngx-translate (de/en/tr)

Bu workspace **iki uygulama** ve **bir paylaşılan katman** barındırır:

| Proje           | Ne                                      | Kök                     | Render        | Port |
| --------------- | --------------------------------------- | ----------------------- | ------------- | ---- |
| `hotelcore-web` | Yönetim paneli (giriş arkasında)        | `src/`                  | SPA + PWA     | 4200 |
| `guest-web`     | Misafire açık rezervasyon sitesi        | `projects/guest-web/`   | SSR/prerender | 4300 |
| `shared`        | Marka işareti, dil sözleşmesi, tokenlar | `projects/shared/`      | —             | —    |

Neden iki uygulama: misafir tarafı SEO için sunucu render'ı ister, panel istemez
(giriş arkasındaki ekranları sunucuda render etmek kazanç değil sızıntı
yüzeyidir). Ayrı paket aynı zamanda en temiz güvenlik sınırıdır — misafir,
panelin kodunu hiç indirmez.

> Kod ve teknik isimler İngilizce, kod yorumları Türkçe.
> UI metinleri **yalnızca** i18n dosyalarında; template'lerde sabit metin yoktur.
> Panel: `public/i18n/*.json` (HTTP ile yüklenir) — misafir sitesi:
> `projects/guest-web/src/i18n/*.json` (pakete gömülür, SSR çıktısında metin bulunsun diye).

## Gereksinimler

| Araç    | Sürüm                                                         |
| ------- | ------------------------------------------------------------- |
| Node.js | **>= 22.22.3** (veya 24.15+/26+) — Angular 22 zorunlu kılıyor |
| npm     | >= 10                                                         |
| Backend | `http://localhost:5080` (Swagger: `/swagger`)                 |

`package.json > engines` alanı bu sınırı kayda geçirir. Daha eski bir Node ile
`ng` komutları "minimum Node.js version" hatası verir.

## Komutlar

```bash
npm install

# Toplu — CI bunları çağırır; üçü de TÜM projeleri kapsar
npm run lint            # ESLint: hotelcore-web + guest-web + shared
npm run test            # Vitest: her iki uygulama, sırayla
npm run test -- --run   # aynısı (Vitest alışkanlığı için kabul edilir)
npm run build           # production build: her iki uygulama

# Tek proje
npm start               # panel dev-server (4200, /api -> localhost:5080)
npm run start:guest     # misafir sitesi dev-server + SSR (4300)
npm run build:admin
npm run build:guest
npm run test:admin
npm run test:guest
npm run test -- --project=guest-web

# Misafir sitesi SSR sunucusu (production build sonrası)
npm run build:guest
SSR_ALLOWED_HOSTS=localhost:4400 PORT=4400 npm run serve:guest:ssr

npm run format          # Prettier
npm run api:generate    # OpenAPI'dan tip-güvenli istemci (backend ayaktayken)
```

> `ng build` / `ng test` proje adı olmadan çalışmaz (Angular 22'de `defaultProject`
> yok). `scripts/run-builds.mjs` ve `scripts/run-tests.mjs` projeleri sırayla
> çalıştırır, ilk hatada durur; böylece CI adımları (`npm run lint|test|build`)
> değişmeden her iki uygulamayı da kapsar.

## Klasör yapısı

### Paylaşılan katman (`@hotelcore/shared`)

```
projects/shared/
├── src/
│   ├── public-api.ts        # sınırın gerekçesi burada yazılı
│   ├── i18n/                # AppLanguage sözleşmesi + LanguageStore (signal)
│   └── ui/brand-mark/       # HS monogramı — iki uygulamada da aynı işaret
├── styles/theme.css         # "Otel Defteri" tokenları + temel katman
└── assets/                  # favicon + ikonlar (her iki uygulamaya kopyalanır)
```

ng-packagr ile derlenmez; `tsconfig.json > paths` üzerinden **kaynak olarak**
paylaşılır (`@hotelcore/shared`). Paket npm'e yayınlanmadığı için ikinci bir
derleme hattının maliyeti karşılıksızdır.

Sınır: **durum ve sözleşme paylaşılır, politika paylaşılmaz.** `LanguageStore`
ortaktır; dilin nereden okunacağı değildir — panelde `localStorage`, misafir
sitesinde URL öneki. Yerleşim/kabuk bileşenleri de paylaşılmaz.

### Misafir sitesi (`guest-web`)

```
projects/guest-web/
├── public/robots.txt
└── src/
    ├── i18n/                    # de/en/tr — pakete gömülür (SSR için)
    ├── environments/            # siteOrigin (canonical/hreflang mutlak adres ister)
    ├── index.html  main.ts  main.server.ts  server.ts  styles.css
    ├── testing/                 # ortak TestBed + router harness yardımcısı
    └── app/
        ├── app.routes.ts        # /:lang altındaki sayfa ağacı
        ├── app.routes.server.ts # rota başına render modu (prerender/SSR/CSR)
        ├── core/
        │   ├── i18n/            # dil URL'i, guard'lar, paket içi çeviri yükleyici
        │   ├── routing/         # CurrentUrlStore (dil seçici için)
        │   └── seo/             # canonical + hreflang + title/description
        ├── layout/              # guest-shell, guest-header, guest-footer
        ├── shared/ui/           # media-frame (CLS disiplini), page-intro
        └── features/            # home, search, room-type, booking,
                                 # confirmation, legal, errors
```

### Yönetim paneli (`hotelcore-web`)

```
src/
├── environments/            # apiBaseUrl, service worker anahtarı
├── styles.css               # Tailwind v4 girişi + @theme tasarım tokenları
├── test-setup.ts            # Vitest kurulum dosyası
└── app/
    ├── core/                # tek örnekli altyapı
    │   ├── api/             # HTTP istemcileri (+ üretilen istemcinin yeri)
    │   ├── guards/          # authGuard, guestGuard, permissionGuard
    │   ├── interceptors/    # auth, X-Hotel-Id, Accept-Language, ProblemDetails
    │   ├── models/          # auth, hotel, language, permission, paging, problem details
    │   ├── services/        # auth, token storage, current hotel, language, title strategy
    │   └── state/           # AuthStore, LanguageStore, NotificationStore (signal store)
    ├── shared/              # yeniden kullanılabilir UI, pipe, direktif
    │   ├── directives/      # *hcHasPermission
    │   ├── pipes/           # hcDate, hcMoney (locale'e duyarlı)
    │   └── ui/              # button, card, badge, table-shell, empty-state, spinner, page-header
    ├── layout/              # shell, sidebar, topbar, hotel-switcher, language-picker, user-menu
    └── features/            # lazy-loaded modüller (her biri kendi *.routes.ts dosyasıyla)
        ├── auth/login  dashboard  rooms  reservations  housekeeping
        ├── invoices  employees  vacations  time-tracking  shifts
        └── reports  settings  errors (404 / 403)
```

## Durum yönetimi

NgRx **yok**. `@Injectable({ providedIn: 'root' })` + `signal` / `computed` ile
hafif store'lar kullanılır: private `signal`, public `readonly` computed, açık
aksiyon metotları. Store'lar saf durumdur; HTTP çağrıları `core/api` +
`core/services` katmanındadır.

## Tasarım sistemi — "Otel Defteri"

Tüm tokenlar `projects/shared/styles/theme.css` içindeki `@theme` bloğunda; her
iki uygulama bu dosyayı import eder. Ayrı `tailwind.config.js` yoktur.
Sınıf taraması otomatik değildir (`@import 'tailwindcss' source(none)` + açık
`@source` listesi): böylece her uygulamanın CSS'i yalnızca kendi kaynağından
üretilir, diğerinin sınıfları sızmaz.

- Zemin `--color-paper` `#f4f1ea`, mürekkep `--color-ink` `#16150f`
- Aksan: lacivert `--color-navy` `#1f3a5f`, bakır `--color-copper` `#a9662f`
- Opsiyon/bekleyen: pirinç `--color-brass` `#8f6b2e`
- Ayraçlar: `--color-rule` / `--color-rule-strong` (yalnızca 1px)
- Başlık `--font-serif` (Instrument Serif), etiket & sayı `--font-mono` (IBM Plex Mono),
  gövde `--font-sans` (IBM Plex Sans), sayılar `tabular-nums`
- `--radius-*` ve `--shadow-*` namespace'leri `initial` ile **boşaltıldı**:
  `rounded-*` / `shadow-*` sınıfları hiç üretilmez, kural araç seviyesinde zorlanır.
- Emoji, gradyan, pill buton ve stok ikon seti kullanılmaz; ikon yerine ince
  çizgi ve tipografi (`≡`, `✕`, `▾` gibi tipografik işaretler).

### Misafir tarafı yorumu

Aynı sistem, farklı ton. **Korunanlar:** yuvarlak köşe / gölge / gradyan / emoji /
stok ikon yasağı, 1px cetvel disiplini, aynı yazı aileleri, aynı marka işareti,
fiyat ve tarihlerde `tabular-nums`. **Gevşeyenler** (gerekçeleriyle birlikte
`projects/guest-web/src/styles.css` başında yazılı): daha açık zemin
(`--color-canvas`), editoryal tipografik ölçek (`--text-display/headline/lede`),
geniş bölüm ritmi (`--spacing-section`), büyük harf kullanımının yalnızca
eyebrow/sayı/etiketle sınırlanması, tek ve baskın birincil eylem (`.hcg-action`),
medyanın birinci sınıf öğe olması (`hcg-media-frame`).

## i18n

- Diller: `de` (varsayılan), `en`, `tr`.
- **Panel:** `public/i18n/*.json`, HTTP ile yüklenir. Seçim sırası:
  `localStorage` → tarayıcı dili → `de`. `<html lang|dir>` güncellenir ve
  `Accept-Language` başlığı otomatik gönderilir.
- **Misafir sitesi:** dil **URL'dedir** (`/de/…`, `/en/…`, `/tr/…`) ve tek doğru
  kaynak odur; yerel depoya yazılmaz. Çeviriler pakete gömülür (dil başına ayrı
  chunk), böylece sunucudan çıkan HTML'de metin bulunur. Ön eki olmayan her adres
  `Accept-Language` (SSR) veya `navigator.languages` (tarayıcı) ile pazarlık
  edilip **302 ile** dil önekli adrese yönlendirilir. Her sayfa `canonical` +
  üç `hreflang` + `x-default` bağı yayar.

## SEO ve render modları (misafir sitesi)

| Rota                         | Mod       | Neden                                            |
| ---------------------------- | --------- | ------------------------------------------------ |
| `/:lang`                      | Prerender | herkes için aynı, nadiren değişir                |
| `/:lang/legal/*`              | Prerender | statik, her istekte üretmenin anlamı yok         |
| `/:lang/rooms/:slug`          | SSR       | fiyat/müsaitlik canlı; slug listesi derlemede yok |
| `/:lang/search`               | SSR       | sorgu bağımlı; `noindex, follow`                 |
| `/:lang/booking`              | Client    | kişisel veri — sunucuya hiç uğramaz              |
| `/:lang/confirmation/:ref`    | Client    | kişisel veri                                      |
| `/:lang/**`                   | SSR (404) | yerelleştirilmiş 404, gerçek 404 durum kodu ile  |
| `/**`                         | SSR (302) | dil pazarlığı + yönlendirme                      |

## Erişilebilirlik

Semantic HTML, "içeriğe atla" bağlantısı, `aria-*` nitelikleri, görünür
`:focus-visible` halkası, ≥ 44×44px dokunmatik hedefler, klavye ile tam gezinme.
Her ekran 375 / 768 / 1440px genişliklerinde doğrulanır.

## PWA

`public/manifest.webmanifest` + `ngsw-config.json` — **yalnızca yönetim paneli**.
Service worker yalnızca production build'de etkindir (`environment.enableServiceWorker`).

Misafir sitesinde service worker **yoktur** ve bilinçli olarak eklenmemiştir:
fiyat ve müsaitlik canlı veridir; onbelleğe alınmış bir sayfanın eski fiyatı
göstermesi, kazandırdığı hızdan pahalıdır.
