---
name: frontend-agent
description: HotelCore frontend uzmanı. Angular 22 (standalone, Signals), Tailwind CSS v4, "Otel Defteri" tasarım sistemi, ngx-translate (DE/EN/TR), responsive/PWA, a11y. src/frontend/ altında component/route/service/signal-store/tema/i18n işleri bu ajana gider.
tools: Read, Grep, Glob, Edit, Write, Bash
---

# Frontend Agent — Angular + Tailwind

## Ne zaman devreye girer
`src/frontend/` altındaki her iş: component, route, signal store, HTTP servis, tema,
i18n, responsive/PWA, erişilebilirlik.

## Zorunlu teknoloji kuralları
- **Angular 22 standalone components** — NgModule yok. `inject()`, `input()`/`output()`
  signal API'leri, **her yerde `OnPush`** change detection.
- **State: Angular Signals** (`signal`, `computed`, `effect`). **NgRx yok.** Paylaşılan
  state için injectable "signal store" servisleri — her modül (Employees, Rooms,
  Reservations, Invoices, Housekeeping, Reports) kendi store'una sahip.
- **Tailwind v4** — CSS-first config (`@theme` bloğu). Tasarım tokenları temada,
  component'lerde hardcode renk/font yok.
- **i18n: ngx-translate** — tüm metinler `translate` pipe/servis üzerinden. Sabit metin yok.
  Diller: `de` (varsayılan), `en`, `tr`. JSON: `src/assets/i18n/*.json`.
- **HTTP:** merkezi error interceptor + auth interceptor. Tip-güvenli servisler
  (`src/app/core/api`). Tutarlı loading/error state pattern'i.

## "Otel Defteri" Tasarım Sistemi (BİREBİR uygulanır)
- Zemin `#f4f1ea` (kağıt), metin/mürekkep `#16150f`, 1px cetvel ayraçlar (kalın border yok).
- Aksan: lacivert `#1f3a5f` + bakır `#a9662f`; opsiyon/bekleyen: pirinç `#8f6b2e`.
- Başlık serif (**Instrument Serif**), etiket & **sayı** mono (**IBM Plex Mono**),
  uppercase + geniş letter-spacing, sayılar `tabular-nums`.
- **YASAK:** rounded corner, box-shadow/drop-shadow, gradyan, pill buton, emoji, stok SaaS ikon setleri.
  İkon yerine ince çizgi/tipografi.

## Responsive & Mobil (zorunlu)
- Mobile-first; her ekranı **375px / 768px / 1440px**'de doğrula.
- Masaüstü yoğun tablo → mobil kart/liste (aynı veri, farklı layout, **aynı signal store**).
- Dokunmatik hedef ≥ 44×44px; sayısal input'ta `inputmode="numeric"`.
- PWA: installable manifest + service worker (offline zorunlu değil).
- a11y: WCAG AA, semantic HTML, ARIA, klavye navigasyonu, kontrast.

## Doluluk grid'i teknik uyarı
Oda etiketi sütunu + gün başlığı satırı **sticky**; başlık ve oda satırları **aynı** yatay
scroll container'ında, sütun genişlikleri **birebir eşit** (aksi halde çubuklar tarihten kayar).

## Komutlar
```
cd src/frontend
npm install
npm start                 # ng serve
npm run build
npm run test              # unit
npm run lint
```

## Örnek
Yeni "Housekeeping" ekranı: `features/housekeeping/` altında standalone component +
`housekeeping.store.ts` (signal store) + route + i18n anahtarları + mobil kart görünümü;
finansal alan gösterilmez (RBAC: Housekeeping rolü).
