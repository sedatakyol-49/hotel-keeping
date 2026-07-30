# HotelCore

Çoklu otel (multi-property), çok dilli (DE/EN/TR), rol + izin (RBAC) tabanlı ve
GoBD-uyumlu faturalama içeren, **agentic (multi-agent)** mimariyle organize edilmiş bir
**otel yönetim sistemi**.

> `HotelCore` yalnızca **kod/repo seviyesi** isimdir. Müşteriye görünen marka/otel adı
> **hardcode edilmez** — Head Office seviyesinde marka adı + her otelin kendi adı olarak
> admin panelden yönetilir.

## Mimari Özet

| Katman | Teknoloji |
|---|---|
| Frontend | Angular 22 (standalone, Signals), Tailwind CSS v4, ngx-translate |
| Backend | .NET 10, ASP.NET Core Web API, Clean Architecture (Domain/Application/Infrastructure/Api) |
| Veritabanı | PostgreSQL + Entity Framework Core 10 (Code-First) |
| Auth | JWT + policy-based RBAC (rol + granüler izin) |
| Mapping / CQRS | Mapster (MIT) + vertical-slice handler'lar (MediatR/AutoMapper kullanılmadı — ticari lisans) |
| CI/CD | GitHub Actions (frontend + backend ayrı job, PostgreSQL service container) |

Detaylar: [docs/architecture.md](docs/architecture.md).

## Modüller

| Modül | API | Ekran | Kapsam |
|---|:--:|:--:|---|
| **Oda Yönetimi** | ✅ | ✅ | RoomType/Room CRUD, çok dilli oda tipi, housekeeping panosu |
| **Ayarlar** | ✅ | ✅ | Otel künyesi, **vergi profili** (KDV + Kurtaxe), marka adı |
| **Personel (Mitarbeiter)** | ✅ | ✅ | Çalışan + departman, çalışma şekli, personel numarası |
| **İzin (Urlaub)** | ✅ | 🔄 | Talep/onay/ret/iptal, yıllık bakiye (onayda düşer, iptalde geri gelir) |
| **Zeiterfassung** | ✅ | 🔄 | Manuel giriş/çıkış, mola, çalışılan süre, manuel düzeltme |
| **Vardiya (Schichtplan)** | ✅ | 🔄 | ISO hafta bazlı plan, gün başına tek vardiya |
| **Rezervasyon** | 🔄 | ⏳ | Misafir, fiyat planı, müsaitlik, check-in/out, doluluk planı |
| **Rechnung (Faturalama)** | 🔄 | ⏳ | GoBD: kesintisiz numara, değiştirilemezlik, Stornorechnung, denetim izi |
| **Raporlama** | ⏳ | ⏳ | Doluluk, ciro, kanal dağılımı, ADR/RevPAR |

✅ bitti · 🔄 geliştiriliyor · ⏳ planlı

Sözleşmelerin tamamı [docs/api-contracts.md](docs/api-contracts.md)'de; mimari kararlar ve
gerekçeleri [docs/architecture.md](docs/architecture.md) §10 karar günlüğünde.

### Bilinen sınırlar
- İzin günü hesabı **takvim günüdür**; hafta sonu ve resmî tatil mantığı bu fazda yoktur.
- Fatura **PDF çıktısı** henüz üretilmez (`GET /invoices/{id}/pdf` → 501); ZUGFeRD/XRechnung için
  `IInvoiceExporter` zemini hazırdır.

## Multi-Agent Yapı
Proje `.claude/` altında ajan ve skill tanımlarıyla organize edilmiştir:
- `.claude/agents/` — Main (orkestratör), Frontend, Backend, Database, DevOps ajanları
- `.claude/skills/` — tekrar kullanılabilir yetenekler (5 skill)
- Detay: [docs/agent-responsibilities.md](docs/agent-responsibilities.md)

## Klasör Yapısı
```
hotel-core/
├── .claude/{agents,skills}/           # multi-agent tanımları
├── .github/workflows/                 # CI/CD (backend-ci.yml, frontend-ci.yml)
├── docs/                              # architecture, agent-responsibilities, api-contracts
└── src/
    ├── backend/                       # HotelCore.slnx (.NET 10)
    │   ├── Directory.Build.props      # ortak derleme ayarları
    │   ├── Directory.Packages.props   # merkezi paket sürümleri (CPM)
    │   ├── global.json                # SDK sabitlemesi
    │   ├── HotelCore.{Domain,Application,Infrastructure,Api}/
    │   └── tests/HotelCore.{Domain,Application,Api.Integration}Tests/
    └── frontend/                      # Angular 22 workspace (hotelcore-web)
```

## Kurulum

### Önkoşullar
- **.NET SDK 10** (`global.json`: 10.0.301)
- **Node.js ≥ 22.22.3** — Angular 22'nin zorunlu alt sınırı; `src/frontend/.nvmrc` = `22.23.1`
- **PostgreSQL 16+** (veya Docker)

### Backend
```bash
cd src/backend
dotnet tool restore                        # proje-yerel dotnet-ef
dotnet restore
# Gizli connection string (plaintext commit etme):
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Database=HotelDb;Username=postgres;Password=***" \
  --project HotelCore.Api
# JWT imzalama anahtarı (en az 32 karakter):
dotnet user-secrets set "Jwt:Secret" "<rastgele-64-karakter>" --project HotelCore.Api

dotnet ef database update --project HotelCore.Infrastructure --startup-project HotelCore.Api
dotnet run --project HotelCore.Api        # Swagger: http://localhost:5080/swagger
```
> `dotnet ef` connection string'i şu sırayla arar: `ConnectionStrings__Default` env var →
> Api projesinin user-secrets'ı → design-time yer tutucu. Yani yukarıdaki user-secrets
> ayarından sonra ek ortam değişkeni gerekmez.

Development'ta uygulama açılışta bekleyen migration'ları uygular ve demo veriyi seed eder
(`admin@hotelcore.local` / `Admin!23` — **yalnızca Development**).

### Frontend
```bash
cd src/frontend
npm ci
npm start                                  # http://localhost:4200 (dev proxy → :5080)
```

### Docker (PostgreSQL — hızlı başlangıç)
```bash
docker run --name hotelcore-pg -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=hotelcore -p 5432:5432 -d postgres:16
```

## Test
```bash
cd src/backend  && dotnet test               # xUnit + AwesomeAssertions + NSubstitute
cd src/frontend && npm run test -- --run     # Vitest
```
Integration testler bir PostgreSQL ister. Sırayla arar: `ConnectionStrings__Default` env var →
Docker (Testcontainers `postgres:16-alpine`) → ikisi de yoksa **skip** edilir.
```bash
# Ayrı bir test veritabanına karşı çalıştırma:
ConnectionStrings__Default="Host=localhost;Database=HotelDb_IntegrationTests;Username=postgres;Password=***" \
  dotnet test tests/HotelCore.Api.IntegrationTests
```

## Güvenlik / Secrets
- Connection string ve JWT secret **asla repoya commit edilmez** — user-secrets /
  environment variable (`ConnectionStrings__Default`, `Jwt__Secret`).
- `appsettings.json` yalnızca placeholder içerir.

## CI/CD & Branch Protection (öneri)
- PR açıldığında `backend-ci.yml` (restore → build `-warnaserror` → unit test → PostgreSQL 16
  service container'a karşı `ef database update` + integration test) ve `frontend-ci.yml`
  (`npm ci` → lint → test → production build) çalışır.
- Her iki workflow'da `paths` filtresi var: sadece frontend değişen bir PR'da backend-ci **hiç
  koşmaz**. Bu yüzden ikisini birden "required status check" yaparsanız o PR merge edilemez —
  check'leri ayrı ayrı yönetin veya tek bir gate workflow'u kullanın.
- `main` branch'te **branch protection** önerilir: testler/build geçmeden merge yok +
  "require branches to be up to date" + linear history.

## Dil Politikası
Kod ve teknik isimler (entity/endpoint/commit) **İngilizce**; README ve yorum açıklamaları
**Türkçe**.

## Lisans Notu
Ticari lisansa geçen kütüphaneler kullanılmadı; MIT alternatifleri tercih edildi:

| Kullanılmayan | Yerine |
|---|---|
| MediatR | Kendi vertical-slice `IDispatcher`'ımız (`Application/Common/Messaging/`) |
| AutoMapper | Mapster |
| FluentAssertions (v8+) | AwesomeAssertions (FluentAssertions 7'nin MIT fork'u) |
