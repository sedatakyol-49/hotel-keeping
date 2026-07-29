---
name: frontend-angular-tailwind
description: Angular 22 (standalone + Signals) + Tailwind v4 + ngx-translate ile HotelCore frontend geliştirme. Component, signal store, route/guard, tema, i18n, responsive/PWA işlerinde tetiklenir.
---

# Skill: Frontend (Angular + Tailwind)

## Tetikleyici senaryolar
- Yeni ekran/component, route, signal store, HTTP servis.
- "Otel Defteri" tasarım sistemi uygulaması, tema tokenı.
- i18n anahtarı ekleme, dil desteği.
- Responsive/mobil layout, PWA, a11y.

## Konvansiyonlar
- **Klasör:** `features/<module>/` (component + `<module>.store.ts` + routes),
  `core/` (interceptors, guards, api client, config), `shared/` (reusable component/pipe/directive).
- Standalone component, `changeDetection: OnPush`, `inject()`, `input()`/`output()` signal API.
- Signal store: `@Injectable({providedIn:'root'})`, private `signal`, public `computed`,
  aksiyon metodları. NgRx yok.
- Metin: `{{ 'module.key' | translate }}`. Sabit metin yasak.
- Renk/font: Tailwind `@theme` tokenları (`bg-paper`, `text-ink`, `font-serif`, `font-mono`).
  Hardcode hex yasak.

## Örnek: signal store
```ts
@Injectable({ providedIn: 'root' })
export class ReservationsStore {
  private readonly _items = signal<Reservation[]>([]);
  private readonly _loading = signal(false);
  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly count = computed(() => this._items().length);

  constructor(private api: ReservationsApi) {}
  async load(range: DateRange) {
    this._loading.set(true);
    try { this._items.set(await firstValueFrom(this.api.list(range))); }
    finally { this._loading.set(false); }
  }
}
```

## Komutlar
`npm start`, `npm run build`, `npm run test`, `npm run lint` (cwd: `src/frontend`).

## Kontrol listesi (her ekran için)
- [ ] 375 / 768 / 1440px'de doğrulandı
- [ ] Tüm metin i18n'de (de/en/tr)
- [ ] RBAC: izinsiz alanlar gizli
- [ ] a11y: semantic HTML + ARIA + klavye
- [ ] Tasarım sistemi: rounded/shadow/gradient/emoji YOK
