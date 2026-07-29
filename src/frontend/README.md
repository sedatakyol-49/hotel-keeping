# HotelCore — Frontend (`hotelcore-web`)

Angular 22 (standalone + Signals) · Tailwind CSS v4 (CSS-first) · ngx-translate (de/en/tr)

> Kod ve teknik isimler İngilizce, kod yorumları Türkçe.
> UI metinleri **yalnızca** `public/i18n/*.json` içinde; template'lerde sabit metin yoktur.

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
npm start            # ng serve (dev proxy: /api -> http://localhost:5080)
npm run build        # production build
npm run lint         # ESLint (angular-eslint + a11y şablon kuralları)
npm run test         # Vitest, tek seferlik
npm run test -- --run   # aynısı (Vitest alışkanlığı için kabul edilir)
npm run test:watch   # izleme modu
npm run format       # Prettier
npm run api:generate # OpenAPI'dan tip-güvenli istemci (backend ayaktayken)
```

## Klasör yapısı

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

Tüm tokenlar `src/styles.css` içindeki `@theme` bloğunda. Ayrı `tailwind.config.js` yoktur.

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

## i18n

- Diller: `de` (varsayılan), `en`, `tr` — dosyalar `public/i18n/*.json`.
- Seçim sırası: `localStorage` → tarayıcı dili → `de`.
- Dil değişimi sayfa yenilemeden uygulanır; `<html lang|dir>` güncellenir ve
  `Accept-Language` başlığı otomatik gönderilir.

## Erişilebilirlik

Semantic HTML, "içeriğe atla" bağlantısı, `aria-*` nitelikleri, görünür
`:focus-visible` halkası, ≥ 44×44px dokunmatik hedefler, klavye ile tam gezinme.
Her ekran 375 / 768 / 1440px genişliklerinde doğrulanır.

## PWA

`public/manifest.webmanifest` + `ngsw-config.json`. Service worker yalnızca
production build'de etkindir (`environment.enableServiceWorker`).
