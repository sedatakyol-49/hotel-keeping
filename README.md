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
| **Public Rezervasyon** | ⏳ | ⏳ | Misafire açık online kanal: arama, fiyat teklifi, hold, rezervasyon, iptal (ayrı Angular uygulaması, SSR) |

✅ bitti · 🔄 geliştiriliyor · ⏳ planlı

Sözleşmelerin tamamı [docs/api-contracts.md](docs/api-contracts.md)'de; mimari kararlar ve
gerekçeleri [docs/architecture.md](docs/architecture.md) §10 karar günlüğünde.

**Misafire açık (public) rezervasyon kanalı** kendi belgelerinde tanımlıdır:
[docs/architecture-public-booking.md](docs/architecture-public-booking.md) (mimari, workspace,
SSR, güvenlik ve mevzuat eşlemesi) ve
[docs/api-contracts-public-booking.md](docs/api-contracts-public-booking.md) (uç uç sözleşme).

### Bilinen sınırlar
- İzin günü hesabı **takvim günüdür**; hafta sonu ve resmî tatil mantığı bu fazda yoktur.
- Fatura **PDF çıktısı** henüz üretilmez (`GET /invoices/{id}/pdf` → 501); ZUGFeRD/XRechnung için
  `IInvoiceExporter` zemini hazırdır.

### Canlıya çıkmadan mali onay isteyen kararlar
Aşağıdaki dört karar kodda uygulanmış ve tek noktada toplanmış durumda, ancak **mali müşavir
onayı** alınmadan üretimde kullanılmamalı:

| Karar | Uygulanan varsayım | Değişecek tek yer |
|---|---|---|
| Fiyat tabanı | Birim fiyatlar **brüt**; KDV içinden çıkarılır (PAngV + `Reservation.TotalAmount` brüt tanımı) | `InvoiceAmounts.ComputeLine` |
| Kurtaxe ve KDV | Şehir vergisi **KDV dışı** (belediyenin misafirden aldığı, otelin yalnızca tahsil ettiği tutar) | `InvoiceAmounts.ResolveVatRate` |
| KDV oranı eşlemesi | Konaklama **indirimli** oran, ekstralar **standart** oran (kahvaltı Aufteilungsgebot gereği indirimli orandan yararlanmaz) | aynı yer |
| No-show / iptal bedelinin KDV'si | No-show faturasında konaklama satırı **korunur** ve **%7 indirimli oranla** KDV'lenir (satır türü `RoomCharge` kaldığı için). Kurtaxe satırı ise **hiç üretilmez** | `InvoiceAmounts.ResolveVatRate` + satır türü |

Bazı şehirlerin "Bettensteuer" uygulamasında idare şehir vergisini bedelin parçası sayabiliyor;
bu belediye bazında değerlendirilmelidir. Kurtaxe **çocuk muafiyeti** otel bazında açılabilir
(`TaxProfile.CityTaxExemptChildren`); rezervasyon yalnızca yetişkin/çocuk **sayısı** tuttuğu için
yaş sınırı hesaba girmez, hukuki dayanağı belgelemek için saklanır.

**No-show / iptal bedeli: KDV'ye tabi mi?** Bugünkü davranış (konaklama satırının %7 ile
faturalanması) **muhtemelen yanlıştır** ve canlıya çıkmadan mali müşavire sorulmalıdır.

- **Baskın Alman görüşü:** misafirin gelmemesi veya iptali hâlinde alınan bedel **gerçek
  tazminattır** (*echter Schadensersatz*) ve **KDV'ye tabi değildir**, çünkü karşılığında bir
  teslim/hizmet yoktur — UStG §1 Abs. 1 Nr. 1 anlamında bir *Leistungsaustausch* doğmaz. Bu görüş
  uygulanırsa satır **KDV'siz** kesilmeli, belgede tazminat olduğu belirtilmeli ve satır
  **`RoomCharge` olarak kalmamalıdır** — aksi hâlde indirimli oranın konaklama gerekçesi
  (UStG §12 Abs. 2 Nr. 11) belgeyle çelişir.
- **Karşı görüş:** garantili/ön ödemeli rezervasyonlarda otelin odayı hazır tutması başlı başına
  bir edim sayılabilir; o zaman bedel bir hizmetin karşılığı olur ve KDV'ye tabi olur. Hangi oranın
  uygulanacağı ayrıca tartışmalıdır.
- **Ayrım kriteri sözleşmedir:** bedelin *tazminat* mı *bedel* mi olduğu otelin AGB'sindeki ifadeye
  ve garantinin niteliğine bağlıdır. HotelCore bu fazda **hiçbir varsayımı zorlamaz**.
- **Kurtaxe bundan bağımsızdır ve kapatılmıştır:** `NoShow`/`Cancelled` rezervasyonda Kurtaxe satırı
  **hiç üretilmez** — Kurbeitragssatzung'ların vergiyi doğuran olayı fiilî *Übernachtung*'dur ve otel
  belediye adına tahsil ettiği tutarı (UStG §10 Abs. 1 Satz 5, *durchlaufender Posten*) misafirden
  isteyemez.

### Tüketiciye açık satış — canlıya çıkmadan hukuki onay isteyen kararlar
Public rezervasyon kanalı (planlı modül) tüketici hukukunun admin tarafında hiç karşılaşılmayan
zorunluluklarını getirir. Aşağıdaki yedi karar sözleşmede **uygulanmış varsayım** olarak yazılmıştır;
**avukat/hukukçu onayı** alınmadan üretimde kullanılmamalıdır. Tam gerekçeler ve karşı görüşler:
[docs/architecture-public-booking.md](docs/architecture-public-booking.md) §10.

| Karar | Uygulanan varsayım | Değişecek tek yer |
|---|---|---|
| Button-Lösung (§312j Abs. 3 BGB) | Ödeme otelde yapılsa da düğme **`zahlungspflichtig buchen`** olmalı; sunucu metni doğrulamaz, **kanıt olarak dondurur** | `legal.orderButton.payable` i18n anahtarı |
| §312f kalıcı veri taşıyıcısı | E-posta yeterlidir, **içerik gövdede** olmak kaydıyla (yalnızca bağlantı değil) | `IBookingConfirmationSender` şablonu |
| Sözleşmenin kurulma anı | **Anında onay = kabul** (onay e-postası *Annahme*'dir) | `PublicBookingSettings.ConfirmationMode` |
| Sözleşmenin tarafı | **Otel** (fatura da otel adına kesiliyor), marka/Head Office değil | `HotelLegalProfile` düzeyi |
| Kurtaxe'nin gösterimi (PAngV) | Toplam fiyatın **içinde** *ve ayrıca* ayrı satır olarak | `price.totalGross` bileşimi |
| Geç iptal / no-show bedelinin KDV'si | **Karar verilmedi, bilinçli olarak** — public sözleşme yalnızca tutarı bildirir; yukarıdaki fatura tartışması aynen geçerlidir | `InvoiceAmounts.ResolveVatRate` |
| DSGVO Art. 17 self-servis silme | **Bu fazda yok**; faturalanmış konaklama GoBD/AO §147 gereği 10 yıl saklanır, talepler manuel işlenir | Ayrı faz |

Ayrıca **§312g Abs. 2 Nr. 9 BGB**: tarihli konaklamada 14 günlük cayma hakkı **yoktur**, ama bu
misafire bildirilmek zorundadır — genel bir *Widerrufsbelehrung* göstermek yanlış olur. Sözleşme
bunu ayrı bir `legal.withdrawalRight` bildirimi olarak taşır ve onaylanan **versiyonu dondurur**.

**Kart verisi hiçbir koşulda veritabanımıza yazılmaz.** Public uçlarda kart alanı yoktur; gövdede
böyle bir alan adı geçerse istek reddedilir ve gövde loglanmaz. PCI-DSS kapsamı dışında kalmanın
tek yolu budur.

### §14 UStG zorunlu fatura içeriği — bilinen eksikler
Fatura verisi §14 Abs. 4 UStG'ye karşı denetlendi. **Orana göre ayrıştırılmış tutarlar** (Nr. 8),
düzenleyen/alıcı künyesi (Nr. 1), vergi numarası (Nr. 2) ve hizmet dönemi (Nr. 6) eklendi. Şema
gerektirdiği için **yapılmayanlar**, belge (PDF) üretimine geçmeden önce kapatılmalıdır:

- `Guest` adresinde **ülke** yok (`Nationality` uyrukluktur, adres ülkesi değildir).
- `Hotel.TaxNumber` tek serbest metin: **Steuernummer** ile **USt-IdNr.** ayrılmıyor (§14 Abs. 4
  Nr. 2; AB içi hizmetlerde §14a UStG USt-IdNr. zorunlu kılar).
- **Künye dondurulmuyor:** otel/misafir adresi değişirse eski faturalar yeni adresle görünür — GoBD
  *Unveränderbarkeit* riski.
- **İndirim alanı** (Nr. 7) ve **vergi muafiyeti sebebi** (Nr. 8) modellenmedi.
- **§33 UStDV** (Kleinbetragsrechnung, 250 €) modellenmedi; eşik bir mevzuat parametresi olduğu için
  koda gömülmemeli, yapılandırmadan gelmelidir.
- Erken çıkışta Kurtaxe **fiilî geceye** göre hesaplanmalıdır; bunun için rezervasyonda fiilî
  giriş/çıkış **takvim günü** ve otelde **saat dilimi** tutulması gerekir (UTC anını güne indirgemek
  gün sınırında bir geceyi kaydırır ve yanlış beyan üretir).

> Bu eksiklerden ikisi **public rezervasyon kanalı** çalışmasında kapatılır: `Hotel.VatId`
> (USt-IdNr., `TaxNumber` = Steuernummer'dan ayrı — §5 DDG Impressum de bunu ister) ve
> `Hotel.TimeZoneId` (IANA). Saat dilimi eklenince erken çıkış eksiğinin **yarısı** kapanır;
> fiilî giriş/çıkış **takvim günü** hâlâ tutulmadığı için madde açık kalır.

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
    └── frontend/                      # Angular 22 workspace
        ├── src/                       # hotelcore-web — ADMIN uygulaması (CSR)
        └── projects/
            ├── shared/                # paylaşılan kütüphane (@hotelcore/shared)
            └── guest-web/             # MİSAFİR uygulaması (SSR + prerender)
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
