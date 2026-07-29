# HotelCore — Mimari Dokümanı

> Bu doküman, HotelCore otel yönetim sisteminin mimari kararlarını, domain modelini
> ve uyumluluk gereksinimlerini içerir. **Kod ve teknik isimler İngilizce**, açıklamalar
> Türkçe. Main Agent her önemli mimari kararı buraya kısa not olarak düşer.

## 1. Genel Bakış

HotelCore, **çoklu otel (multi-property / multi-tenant)** destekleyen, **çok dilli
(DE/EN/TR)**, **rol + granüler izin (RBAC)** tabanlı, **GoBD-uyumlu faturalama** içeren
bir otel yönetim sistemidir.

| Katman | Teknoloji |
|---|---|
| Frontend | Angular 22 (standalone, Signals), Tailwind CSS v4, ngx-translate |
| Backend | .NET 10, ASP.NET Core Web API, Clean Architecture |
| Data | Entity Framework Core 10, PostgreSQL (Npgsql) |
| Auth | JWT (rol + izin + hotel claim'leri), policy-based authorization |
| Mapping | Mapster (MIT) — *AutoMapper ticari lisansa geçtiği için tercih edilmedi* |
| Mediator | Vertical-slice handler'lar (hafif, kütüphane bağımsız) — *MediatR ticari lisansa geçtiği için* |
| CI/CD | GitHub Actions (frontend + backend ayrı job, PostgreSQL service container) |

## 2. Çözüm Yapısı (Clean Architecture)

```
HotelCore.sln
├── src/backend/
│   ├── HotelCore.Domain          # Entity'ler, enum'lar, domain kuralları — HİÇBİR dış bağımlılık yok
│   ├── HotelCore.Application      # Use-case'ler (handler), DTO, validation, mapping, arayüzler
│   ├── HotelCore.Infrastructure   # EF Core DbContext, PostgreSQL, repository, auth, dış servisler
│   └── HotelCore.Api              # Controllers, middleware, DI kompozisyon kökü, Swagger
└── tests/
    ├── HotelCore.Domain.Tests
    ├── HotelCore.Application.Tests
    └── HotelCore.Api.IntegrationTests
```

**Bağımlılık yönü:** `Api → Infrastructure → Application → Domain`. Domain hiçbir katmana
bağımlı değildir (Dependency Rule). Application katmanı arayüzleri tanımlar (`IAppDbContext`,
`ICurrentUser`, `IJwtTokenService` vb.), Infrastructure bunları implemente eder.

**CQRS kararı:** MediatR ticari lisansa geçtiği için harici bir mediator kütüphanesi yerine
`IRequestHandler<TRequest, TResponse>` arayüzü + DI ile çözülen ince bir `IDispatcher`
kullanıyoruz (vertical-slice). Bu, MediatR API'sinin öğrenme eğrisi olmadan aynı ayrıştırmayı
sağlar ve lisans/gelir-eşiği riski taşımaz. Handler'lar `Application/Features/<Module>/` altında.

## 3. Çoklu Otel (Multi-Tenant) Stratejisi

**Yaklaşım: Paylaşımlı veritabanı + `HotelId` bazlı satır seviyesi izolasyon** (ayrı şema/DB değil).

Gerekçe: Head Office'in tüm otelleri **konsolide** raporlayabilmesi gerekiyor; ayrı DB/şema
bunu pahalı cross-database sorgulara çevirirdi. Tek DB + `HotelId` filtresi + EF Core global
query filter, hem izolasyonu hem konsolidasyonu basit tutar.

- **Hiyerarşi:** `HeadOffice (1) → Hotel (N) → [Room, Employee, Reservation, Invoice, ...]`
- Her tenant-scoped entity `ITenantEntity` arayüzünü (`HotelId` property'si) uygular.
- `AppDbContext` içinde **global query filter** her sorguya otomatik `WHERE HotelId = @current`
  ekler. Aktif otel `ICurrentUser.HotelId` üzerinden okunur (JWT claim'inden).
- **Head Office bypass:** Head Office rolündeki kullanıcı için filtre devre dışı bırakılır
  (`ICurrentUser.CanAccessAllHotels == true`) → tüm oteller görünür. Bu, `IgnoreQueryFilters()`
  yerine filtre koşuluna `|| _currentUser.CanAccessAllHotels` ekleyerek yapılır (yanlışlıkla
  bypass edilmesini önlemek için tek noktadan kontrol).
- Kullanıcı ↔ Otel çoka-çok: `UserHotelAccess` tablosu (bir bölge müdürü birden çok otele
  erişebilir). Aktif otel frontend'deki "hotel switcher"dan seçilir; JWT yenilenmeden
  request header'ı (`X-Hotel-Id`) ile aktif otel değiştirilebilir (erişim yetkisi doğrulanır).

## 4. Domain Modeli

### 4.1 Organizasyon & Otel
| Entity | Açıklama | Ana alanlar |
|---|---|---|
| `HeadOffice` | Üst organizasyon (marka sahibi) | `Id`, `BrandName`, `DefaultCulture` |
| `Hotel` | Fiziksel otel / şube (tenant) | `Id`, `HeadOfficeId`, `Name`, `Country`, `City`, `DefaultCulture`, `Currency`, `TaxProfile` (owned) |
| `TaxProfile` | Otelin vergi profili (owned type) | `VatRate`, `ReducedVatRate`, `CityTaxPerPersonNight` (Kurtaxe), `CityTaxEnabled` |

`Country` bir enum (`DE`, `AT`, `CH`, `TR`, ...); vergi oranları **koda hardcode edilmez**,
`TaxProfile` üzerinden otel bazında yönetilir (admin panel).

### 4.2 Personel (HR)
| Entity | Açıklama |
|---|---|
| `Employee` | Çalışan; `HotelId`, `DepartmentId`, `UserId?` (login ilişkisi), `EmploymentType`, `AnnualLeaveDays` |
| `Department` | Departman (Reception, Housekeeping, Kitchen, Management ...) |
| `VacationRequest` | İzin talebi; `From`, `To`, `Status` (Pending/Approved/Rejected/Cancelled), `ApprovedByUserId` |
| `VacationBalance` | Yıl bazında izin bakiyesi; `Year`, `EntitledDays`, `UsedDays`, `CarriedOverDays` |
| `TimeEntry` | Zeiterfassung — **manuel web** giriş/çıkış; `ClockIn`, `ClockOut`, `BreakMinutes`, `Source=Manual` |
| `Shift` | Vardiya planı; `Date`, `ShiftType` (Morning/Evening/Night/Off), `EmployeeId` |

### 4.3 Oda & Rezervasyon (Odoo PMS'ten uyarlanan kavramlar)
| Odoo Kavramı | HotelCore Karşılığı |
|---|---|
| Property → Room Type → Room | `Hotel` → `RoomType` → `Room` |
| Rate Plan / BAR | `RatePlan` (RoomType'a bağlı, sezon/kanal bazlı fiyat) |
| Folio | `Reservation` + ilişkili `Folio` (konaklama boyunca masraflar) → check-out'ta `Invoice` |
| Housekeeping status | `Room.HousekeepingStatus` (Clean/Dirty/Inspected/OutOfOrder) |
| Availability engine | `IAvailabilityService` — çakışan rezervasyonu engeller |

| Entity | Ana alanlar |
|---|---|
| `RoomType` | `HotelId`, `Name` (çeviri tablosuyla), `BasePrice`, `Capacity`, `SizeSqm`, `Amenities` |
| `Room` | `HotelId`, `RoomTypeId`, `Number`, `Floor`, `HousekeepingStatus`, `IsOutOfOrder` |
| `RatePlan` | `RoomTypeId`, `Name`, `Price`, `ValidFrom`, `ValidTo`, `Channel?` |
| `Reservation` | `HotelId`, `RoomId`, `GuestId`, `CheckIn`, `CheckOut`, `Adults`, `Children`, `Status`, `Channel`, `TotalAmount`, `DepositPercent` |
| `Folio` | `ReservationId`, satırları `InvoiceLineItem` olarak toplar (check-out'a kadar açık hesap) |
| `Guest` | `HotelId`, `FirstName`, `LastName`, `Email`, `Phone`, `Nationality`, geçmiş konaklama sayısı (computed) |

**Reservation.Status:** `Option` (opsiyon, kesikli çizgi) → `Confirmed` → `CheckedIn` (otelde) →
`CheckedOut` → `Cancelled` / `NoShow`. Grid render'ında bu durumlar görsel stillere karşılık gelir.

**Reservation.Channel** (enum/lookup): `Direct`, `Phone`, `WalkIn`, `BookingCom`, `Hrs`,
`Expedia`, `Corporate`. Ciro raporlarında kanal dağılımı + OTA komisyon oranı için kullanılır.

### 4.4 Faturalama (Rechnung) — bkz. §6 GoBD
| Entity | Ana alanlar |
|---|---|
| `Invoice` | `HotelId`, `InvoiceNumber` (boşluksuz sekans), `ReservationId?`, `GuestId`, `IssuedAt`, `Status` (Draft/Finalized/Paid/Cancelled), `Culture`, `Currency`, `NetAmount`, `VatAmount`, `CityTaxAmount`, `GrossAmount`, `CancelledByInvoiceId?` |
| `InvoiceLineItem` | `InvoiceId`, `Type` (RoomCharge/Extra/CityTax), `Description`, `Quantity`, `UnitPrice`, `VatRate`, `LineNet`, `LineVat` |
| `Payment` | `InvoiceId`, `Method` (Cash/Card/Transfer), `Amount`, `PaidAt` |
| `InvoiceAuditEntry` | `InvoiceId`, `Action` (Created/Finalized/Paid/Cancelled), `PerformedByUserId`, `PerformedAt`, `Details` (JSON) — denetim izi |

**InvoiceLineItem.Type = CityTax** → Almanya Kurtaxe (kişi × gece × oran). Oran `Hotel.TaxProfile`
üzerinden okunur.

### 4.5 Kimlik & Yetki
| Entity | Açıklama |
|---|---|
| `User` | Login kimliği; `Email`, `PasswordHash`, `Culture`, `HeadOfficeId` |
| `Role` | Rol; `Name`, `IsHeadOfficeLevel` |
| `Permission` | Granüler izin; `Key` (örn. `Invoices.Approve`) |
| `RolePermission` | Rol ↔ İzin çoka-çok |
| `UserRole` | Kullanıcı ↔ Rol çoka-çok |
| `UserHotelAccess` | Kullanıcı ↔ Otel çoka-çok (hangi otelleri görebilir) |
| `RefreshToken` | Yenileme token'ı; `UserId`, `TokenHash` (SHA-256 — **ham token saklanmaz**), `ExpiresAt`, `RevokedAt?`, `ReplacedByTokenId?` (rotation zinciri). Tenant-scoped **değil** (otel seçmeden yenileme yapılabilmeli) |

### 4.6 Çeviri (dinamik içerik)
DB'den gelen kullanıcı-tanımlı içerikler (örn. `RoomType.Name`, `RoomType.Description`) için
`Translation` tablosu: `(EntityType, EntityId, Field, Culture) → Text`. Statik UI metinleri
**frontend'de ngx-translate JSON** ve **backend'de .resx** ile yönetilir (DB'de değil).

## 5. Modüller Arası İlişkiler

- Bir `Reservation` → check-out'ta bir `Invoice`'a dönüşür (Folio üzerinden).
- Bir `Employee` → `Shift` (vardiya) ve `TimeEntry` (fiili giriş/çıkış) üretir; ikisi
  `EmployeeId` üzerinden ilişkilenir (planlanan vs. gerçekleşen mesai karşılaştırması).
- Bir `VacationRequest` onaylanınca ilgili yılın `VacationBalance.UsedDays` güncellenir.
- `Room.HousekeepingStatus` check-out'ta otomatik `Dirty`'ye geçer (Odoo housekeeping akışı).

## 6. GoBD Uyumluluğu (Fatura)

> GoBD = *Grundsätze zur ordnungsmäßigen Führung und Aufbewahrung von Büchern*. Bu fazda
> **tam uyumluluk yalnızca Almanya (`Hotel.Country == DE`) oteller için** zorunlu; mimari,
> ileride ülkeye özel kurallara (`ICountryComplianceRule`) genişletilebilir tutulmuştur.

1. **Değiştirilemezlik (Unveränderbarkeit):** `Invoice.Status == Finalized` olduğunda
   fatura **update/delete edilemez** (DbContext `SaveChanges` override + domain guard). Düzeltme
   gerekirse `Stornorechnung` (iptal/mahsup faturası) oluşturulur; orijinal korunur,
   `CancelledByInvoiceId` ile bağlanır.
2. **Kesintisiz sıralı numara:** Her otel için ayrı, boşluksuz artan `InvoiceNumber` sekansı.
   PostgreSQL sequence yerine `HotelInvoiceCounter` tablosu + `SELECT ... FOR UPDATE` (satır
   kilidi) ile transaction içinde atanır → atlama/tekrar olmaz.
3. **Denetim izi (Audit Trail):** Her fatura işlemi `InvoiceAuditEntry` olarak kim/ne zaman/ne
   bilgisiyle yazılır (append-only).
4. **Saklama süresi (10 yıl):** Faturalar **gerçek silinmez** — soft-delete (`IsDeleted` +
   global filter). Finalize edilmiş faturalar zaten hard-delete edilemez.
5. **Makine-okunabilirlik:** Fatura hem PDF (insan) hem yapılandırılmış veri olarak saklanır.
   İleride ZUGFeRD/XRechnung (gömülü XML) için `IInvoiceExporter` arayüzü ile zemin hazır;
   bu faz için zorunlu değil.

## 7. RBAC — Rol / İzin Matrisi

Başlangıç izin anahtarları (`Permission.Key`), `Modül.Aksiyon` formatında:

`Hotels.View`, `Hotels.Manage`, `Employees.View`, `Employees.Edit`, `Vacations.View`,
`Vacations.Request`, `Vacations.Approve`, `TimeTracking.View`, `TimeTracking.Record`,
`Shifts.View`, `Shifts.Edit`, `Rooms.View`, `Rooms.Manage`, `Housekeeping.View`,
`Housekeeping.Update`, `Reservations.View`, `Reservations.Create`, `Reservations.CheckInOut`,
`Rates.View`, `Rates.Manage`, `Invoices.View`, `Invoices.Create`, `Invoices.Approve`,
`Invoices.Cancel`, `Reports.View`, `Settings.Manage`.

| İzin \ Rol | Admin | HeadOfficeManager | HotelManager | Receptionist | Housekeeping | Accountant |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Tüm oteller (bypass) | ✅ | ✅ | — | — | — | — |
| Hotels.Manage | ✅ | ✅ | kendi | — | — | — |
| Employees.* | ✅ | ✅ | ✅ | — | — | — |
| Vacations.Approve | ✅ | ✅ | ✅ | — | — | — |
| Vacations.Request | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| TimeTracking.Record | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Shifts.Edit | ✅ | ✅ | ✅ | — | — | — |
| Housekeeping.Update | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| Reservations.* | ✅ | ✅ | ✅ | ✅ | — | — |
| Rates.Manage | ✅ | ✅ | ✅ | — | — | ✅ |
| Invoices.Create | ✅ | ✅ | ✅ | ✅ | — | ✅ |
| Invoices.Approve/Cancel | ✅ | ✅ | ✅ | — | — | ✅ |
| Reports.View (finansal) | ✅ | ✅ | ✅ | — | ❌ | ✅ |
| Settings.Manage | ✅ | ✅ | kendi | — | — | — |

> **Housekeeping rolü finansal veri (fiyat, ciro) görmez** — sadece Kat Hizmetleri +
> kendi zaman/izin kayıtları. Bu hem backend policy'de hem frontend görünürlüğünde uygulanır.

Yetkilendirme **policy-based**: her izin anahtarı bir `AuthorizationPolicy`'ye map edilir
(`options.AddPolicy("Invoices.Approve", p => p.RequireClaim("perm", "Invoices.Approve"))`).
Controller'larda `[Authorize(Policy = "Invoices.Approve")]`. Roller controller'a **hardcode edilmez**.

## 8. i18n Stratejisi
- **Diller:** `de` (varsayılan), `en`, `tr`.
- **Frontend:** ngx-translate — `src/assets/i18n/{de,en,tr}.json`. Runtime dil değişimi
  (sayfa yenilemesiz). Dil kullanıcı profilinde saklanır + tarayıcı diline göre varsayılan.
- **Backend:** `.resx` resource dosyaları (hata/validation/e-posta/fatura PDF metinleri).
  Aktif culture request'ten (`Accept-Language` veya kullanıcı tercihi) okunur.
- **Dinamik içerik:** `Translation` tablosu (§4.6).
- **Format:** Para/tarih locale'e göre (`de-DE`: `1.234,56 €`).

## 9. API Sözleşmesi
Backend Swagger/OpenAPI üretir (`/swagger/v1/swagger.json`). Frontend, tip-güvenli client'ı
bu şemadan üretir (`openapi-generator` / `ng-openapi-gen`). Sözleşme değişiklikleri
`docs/api-contracts.md`'de özetlenir. Detay: bkz. o dosya.

## 10. Karar Günlüğü (Main Agent tarafından güncellenir)
| Tarih | Karar | Gerekçe |
|---|---|---|
| İlk kurulum | Proje adı **HotelCore** | Kullanıcı onayı |
| İlk kurulum | **Mapster + vertical-slice** (MediatR/AutoMapper yok) | Bu iki lib ticari lisansa geçti; MIT alternatif |
| İlk kurulum | **ngx-translate** | Runtime dil değişimi gereksinimi |
| İlk kurulum | Multi-tenant: **paylaşımlı DB + HotelId global query filter** | Head Office konsolide raporlama ihtiyacı |
| İlk kurulum | Vergi oranları **DB'de (TaxProfile), koda hardcode değil** | Ülke/otel bazında değişkenlik |
| 2026-07-29 | Solution formatı **`.slnx`**, test projeleri `src/backend/tests/` altında | .NET 10 yerel XML solution formatı; testler solution köküyle aynı ağaçta olmalı (`cd src/backend && dotnet test`) |
| 2026-07-29 | **Central Package Management** + transitive pinning (`Directory.Packages.props`) | Sürümler tek yerden; Npgsql'in EF Relational 10.0.4 talebi ile bizim 10.0.10 sabitimiz çakışıyordu (MSB3277) |
| 2026-07-29 | **AwesomeAssertions** (FluentAssertions yerine) | FluentAssertions v8+ ticari lisansa geçti; bu MediatR/AutoMapper ile aynı gerekçe. AwesomeAssertions = FluentAssertions 7'nin MIT fork'u |
| 2026-07-29 | Enum'lar veritabanında **string** olarak saklanır | SQL/rapor okunabilirliği + enum sırası değişse de veri bozulmaz |
| 2026-07-29 | Delete davranışı varsayılan **Restrict** (istisna: saf join tabloları ve RefreshToken) | Kazara zincirleme silmeyi önlemek; GoBD gereği `Invoice → InvoiceLineItem` bilinçli olarak Cascade **değil** |
| 2026-07-29 | **`RefreshToken` entity'si eklendi** (§4.5'te yoktu) | `POST /auth/refresh` sözleşmesi rotating + sunucu tarafı iptal edilebilir token gerektiriyor; ham token saklanmaz, SHA-256 hash'i tutulur |
| 2026-07-29 | Concurrency token: `HotelInvoiceCounter.Version` (xmin **değil**) | Npgsql 9+ `UseXminAsConcurrencyToken()` kaldırıldı; gölge xmin property'si PostgreSQL sistem kolonuyla çakışan gerçek kolon üretiyordu |
| 2026-07-29 | RBAC policy'leri `Permissions.All` üzerinden **otomatik üretilir** | Rol/izin adının controller'a hardcode edilmemesi (§7); yeni izin eklenince policy kendiliğinden oluşur |
| 2026-07-29 | Frontend **zoneless** Angular 22 + Signals, Tailwind v4 **CSS-first** (`@theme`) | zone.js yükü yok; `tailwind.config.js` yerine tasarım tokenları tek CSS dosyasında |
| 2026-07-29 | Angular 22 **Node ≥ 22.22.3** gerektirir (`.nvmrc` = 22.23.1) | Geliştirme ortamı kısıtı; CI `node-version-file` ile bunu kullanır |
