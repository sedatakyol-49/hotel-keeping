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
| **Public Rezervasyon** | ✅ | ✅ | Misafire açık online kanal: arama → fiyat teklifi → 15 dk hold → §312j özeti → rezervasyon → onay; sorgulama ve iptal. **Ayrı Angular uygulaması** (`guest-web`, SSR + prerender), 13 uç, üç dil. Kanal **üretimde kapalı gelir** (aşağı bkz.) |

✅ bitti · 🔄 geliştiriliyor · ⏳ planlı

Sözleşmelerin tamamı [docs/api-contracts.md](docs/api-contracts.md)'de; mimari kararlar ve
gerekçeleri [docs/architecture.md](docs/architecture.md) §10 karar günlüğünde.

**Misafire açık (public) rezervasyon kanalı** kendi belgelerinde tanımlıdır:
[docs/architecture-public-booking.md](docs/architecture-public-booking.md) (mimari, workspace,
SSR, güvenlik ve mevzuat eşlemesi) ve
[docs/api-contracts-public-booking.md](docs/api-contracts-public-booking.md) (uç uç sözleşme).
Sözleşme ile **gerçekleşen kod** arasındaki farkların tam listesi: aynı dosyanın
[§13](docs/api-contracts-public-booking.md) bölümü.

> ### Public kanal **kapalı gelir** — açmak bilinçli bir eylemdir
> `PublicBookingSettings.IsEnabled` varsayılanı `false`'tur ve bir migration bunu **açmaz**.
> Kanalı açmak Impressum, AGB ve aydınlatma metninin var olduğunu varsayan **hukuki** bir
> eylemdir; bir şema güncellemesinin yan etkisi olamaz.
> - **Geliştirmede** demo otel (`berlin-mitte`) için kanalı **seed açar**
>   (`PublicChannelSeeder`): slug, saat dilimi, künye, iptal politikası, üç hukuki belge, görsel
>   yer tutucuları ve web'e uygulanabilir bir fiyat planı birlikte yazılır. Seed yalnızca
>   `includeDevelopmentData: true` ile çalışır, o da yalnızca `app.Environment.IsDevelopment()`
>   içinde çağrılır — **üretimde bu blok hiç çalışmaz.**
> - Yapılandırma yalnızca `PublicSlug` boşken uygulanır; sonraki çalıştırmalar elle yapılan
>   ayarları ezmez.
> - **Üretimde** kanal, yönetim panelindeki **Ayarlar** ekranından açılır
>   (`PUT /api/v1/hotels/{id}/settings`, `Settings.Manage`). Slug, saat dilimi ve künye zorunludur;
>   otelin `Website` veya "tüm kanallar" fiyat planı yoksa yanıt `NoRatePlanForWebsiteChannel`
>   uyarısı döndürür (aksi hâlde web fiyatı sessizce oda tipinin liste fiyatına düşer).

### Bilinen sınırlar
- İzin günü hesabı **takvim günüdür**; hafta sonu ve resmî tatil mantığı bu fazda yoktur.
- Fatura **PDF çıktısı** henüz üretilmez (`GET /invoices/{id}/pdf` → 501); ZUGFeRD/XRechnung için
  `IInvoiceExporter` zemini hazırdır.

#### Misafir kanalı — uçtan uca doğrulamadan sonra kalan eksikler
- **Yazı tipleri Google Fonts'tan (gstatic) yükleniyor.** Misafir sitesinin `index.html`'i
  `fonts.googleapis.com`/`fonts.gstatic.com` bağlantısı taşır; Angular derlemede CSS'i satır içine
  alır ama **font dosyaları hâlâ Google'dan** iner, yani ziyaretçinin IP'si onay alınmadan üçüncü
  tarafa gider. Almanya'da bu bilinen bir DSGVO riskidir (LG München I, 20.01.2022 – 3 O 17493/20)
  ve sitenin kendi çerez metniyle de çelişir. **Çözüm:** fontları kendi origin'imizden servis etmek
  (dosyaları depoya almak + lisans notu). Bu tur yapılmadı: ikili varlık eklemek ve font lisansı
  seçmek ayrı bir karardır.
- **Demo görselleri fotoğraf değil, çizimdir.** Yükleme/CDN boru hattı bu fazda yok; seed'in
  işaret ettiği yollarda (`src/frontend/projects/guest-web/public/assets/demo/berlin-mitte/`)
  arayüzün kendi yer tutucu diliyle çizilmiş, **doğru ölçülü** SVG'ler durur. Gerçek fotoğraf
  gerektiğinde bu dosyalar değiştirilir; şema, alan adları ve `width`/`height`/`alt` yolu aynı
  kalır.
- **§312j kanıt ekranı yok.** `GET /reservations/{id}/public-booking` (rıza, gösterilen düğme
  metni, donmuş özet ve fiyat) yalnızca API'de vardır; yönetim panelinde bir ekranı yoktur.
  Uyuşmazlıkta otelin kanıtı bugün ancak API'den okunabilir.
- **Hold, yönetim paneline karşı bağlayıcı değildir.** Ölçülmüş davranış ve gerekçesi:
  [architecture-public-booking.md §5.5](docs/architecture-public-booking.md).
- **Onay e-postası gönderilmiyor:** geliştirme taşıyıcısı belgeyi üretir, özetini kaydeder ve
  gönderimi loglar (`LoggingBookingConfirmationSender`). Bağlantı loglanmaz; dolayısıyla
  geliştirmede `lookup` ile istenen **yeni** erişim bağlantısı hiçbir yerde görünmez.

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
        ├── src/                       # hotelcore-web — ADMIN uygulaması (CSR, :4200)
        ├── scripts/                   # build/test sarmalayıcıları, legal-snapshot.mjs,
        │                              # verify-build-output.mjs (prerender/SSR içerik kapısı)
        └── projects/
            ├── shared/                # paylaşılan kütüphane (@hotelcore/shared)
            └── guest-web/             # MİSAFİR uygulaması (SSR + prerender, :4300)
                ├── public/assets/demo/ # demo yer tutucu görselleri (SVG, seed bunlara işaret eder)
                └── src/generated/     # legal-snapshot.json (ÜRETİLMİŞ — §5 DDG prerender'ı)
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

Development'ta uygulama açılışta bekleyen migration'ları uygular ve demo veriyi seed eder:
demo otel, odalar, personel, **misafire açık kanal** (bkz. yukarıdaki kutu) ve demo yönetici
kullanıcı `admin@hotelcore.local`. Parolası `DbSeeder.DemoAdminPassword` sabitindedir ve buraya
**yazılmaz**; demo kullanıcı yalnızca Development'ta oluşturulur.

### Frontend — **iki uygulama**
Workspace'te iki Angular uygulaması vardır ve ikisi de aynı API'ye bakar:

| Uygulama | Komut | Adres | Not |
|---|---|---|---|
| **Yönetim paneli** (`hotelcore-web`) | `npm start` | http://localhost:4200 | CSR; `/api` istekleri `proxy.conf.json` ile `:5080`'e taşınır |
| **Misafir sitesi** (`guest-web`) | `npm run start:guest` | http://localhost:4300 | SSR + prerender; `/api` istekleri `projects/guest-web/proxy.conf.mjs` ile taşınır |

```bash
cd src/frontend
npm ci
npm start                                  # panel  → http://localhost:4200
npm run start:guest                        # misafir → http://localhost:4300

# Baska bir backend'e bakmak (ornegin ikinci bir ornek):
GUEST_API_TARGET=http://localhost:5081 npm run start:guest
npx ng serve hotelcore-web --port 4201 --proxy-config <kendi-proxy.json>
```

**Aktif otel** misafir sitesinde yapılandırmadan gelir (`environment.hotelSlug` → `GUEST_HOTEL_SLUG`
enjeksiyon belirteci) ve her API çağrısında yola konur; bu tur **otel başına alan adı** dağıtımını
hedefler. Demo slug: `berlin-mitte`.

**Render modu kuralı: fiyat taşıyan hiçbir sayfa prerender edilmez.** Prerender edilen tek şey
hukuki sayfalardır (dil × 3 = **9 sayfa**); ana sayfa, oda tipi detayı ve arama **SSR**'dır, çünkü
katalog "ab" fiyatı taşır ve depoda bayatlayan bir fiyat PAngV açısından yanlış bir iddiadır.
Gerekçe: [architecture-public-booking.md §2.2](docs/architecture-public-booking.md).
Kural derlemede zorlanır — `npm run build` düşen bir prerender isteğinde kırılır ve
`npm run verify:build` prerender kümesini, içeriğini ve SSR çıktısını denetler (SSR denetimi sahte
bir origin kullanır: backend/veritabanı gerekmez).

**Hukuki metinlerin prerender'ı (§5 DDG).** Impressum/AGB/Datenschutz sayfaları derleme anında
üretilir ve içerik HTML'e **gömülür** — JavaScript'siz ziyaretçide de doludur. İçerik, derleme
öncesi alınmış bir anlık görüntüden gelir:

```bash
cd src/frontend
GUEST_API_TARGET=http://localhost:5080 npm run legal:snapshot   # API ayaktayken
npm run legal:snapshot:check                                     # ag gerekmez; CI bunu kosar
```
Hukuki metin değiştiğinde bu betik yeniden çalıştırılmalıdır; gerekçe ve alternatifler:
[architecture-public-booking.md §2.3](docs/architecture-public-booking.md).

**SSR dağıtımı:** `npm run build` sonrası `node dist/guest-web/server/server.mjs`. Gerçek alan
adları `SSR_ALLOWED_HOSTS` (virgüllü) ile verilir; verilmezse sunucu yalnızca `localhost` `Host`
başlığına yanıt verir (mutlak adres zehirlenmesine karşı).

### Demo giriş bilgileri (yalnızca Development)
- **Yönetim paneli:** `admin@hotelcore.local` — parola seed kodundadır
  (`DbSeeder`, `DemoAdminPassword`); **README'ye yazılmaz**, üretimde bu kullanıcı hiç oluşmaz.
- **Misafir sitesi:** giriş **yoktur** ve olmayacaktır. Rezervasyona erişim, onay e-postasındaki
  bağlantıdaki `accessToken` ile olur; bağlantı kaybolursa `/{dil}/manage` ekranından
  rezervasyon referansı + e-posta ile **yeni bir bağlantı** istenir (eskisi geçersiz olur).

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
  service container'a karşı `ef database update` → **"(Pending)" migration yok** +
  **model ile migration ayrışmamış** → integration test) ve `frontend-ci.yml`
  (`npm ci` → lint → **hukuki anlık görüntü denetimi** → test (iki uygulama) → production build
  (iki uygulama + SSR/prerender; **düşen prerender isteği derlemeyi kırar**) →
  **çıktı denetimi**: prerender kümesi yalnızca hukuki sayfalar mı, içerikleri dolu mu, SSR ana
  sayfası katalog + fiyat basıyor mu) çalışır.
- Frontend işi **hiçbir servise bağlı değildir**: hukuki metinler derleme öncesi alınmış
  anlık görüntüden gelir, dolayısıyla CI'da API/veritabanı ve secret gerekmez.
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
