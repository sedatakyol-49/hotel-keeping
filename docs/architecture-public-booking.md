# HotelCore — Mimari Kararı: Misafire Açık (Public) Rezervasyon Kanalı

> Bu belge, sisteme **kendi rezervasyonunu üreten** bir misafir kanalı eklenmesinin mimari
> kararlarını içerir. Uç uç API sözleşmesi ayrı dosyadadır:
> **[api-contracts-public-booking.md](api-contracts-public-booking.md)**.
> Genel mimari için bkz. [architecture.md](architecture.md); bu belge onun §11'idir ve
> §10 Karar Günlüğü'ne ilgili satırlar eklenmiştir.
>
> **Bu belge dört ajanın ortak referansıdır.** Belirsiz bırakılan her nokta dört farklı
> uygulamaya dönüşür; bu yüzden "uygun bir çözüm" denmez, çözümün kendisi yazılır.

---

## 0. Bir cümlelik özet

Misafir tarafı **ayrı bir Angular uygulaması** (SSR/prerender), **ayrı bir public API yüzeyi**
(`/api/v1/public/...`, anonim, otel URL yolundaki `hotelSlug` ile belirlenir), **ayrı DTO'lar**
ve **15 dakikalık geçici tutma (hold)** üzerine kurulur; fiyat ve müsaitlik mevcut
`ReservationPricingService` / `AvailabilityQuery` / `InvoiceAmounts` kodundan **yeniden
kullanılır**, ikinci bir motor yazılmaz; ödeme **girişte** yapılır, kart verisi **hiçbir koşulda**
bizim veritabanımıza girmez.

---

## 1. Neden ayrı bir uygulama (verilmiş karar, gerekçesi belgelenir)

| Kriter | Admin (`hotelcore-web`) | Misafir (`guest-web`) |
|---|---|---|
| Kimlik | JWT zorunlu | **Anonim** |
| Render | CSR (zoneless SPA) | **SSR + prerender** |
| SEO | İstenmez (robots: noindex) | Ürünün varlık sebebi |
| Paket boyutu hedefi | Zengin, oturum sonrası | İlk boya kritik (LCP) |
| Origin | `admin.<host>` | `www.<host>` / otel alan adı |
| CSP | Katı, iç kaynaklar | Katı + harita/PSP için ayrı izinler |

**Güvenlik sınırı gerekçesi (asıl neden):** iki uygulama **farklı origin**'lerde çalışır. Misafir
sitesindeki bir XSS, tarayıcının same-origin politikası gereği admin origin'indeki token
deposuna **erişemez**. Aynı origin'de tek uygulama olsaydı, pazarlama içeriği (görsel, harita,
üçüncü taraf script) taşıyan bir sayfadaki tek bir açık, resepsiyon oturumunu ele geçirmeye
yeterdi. Bu, "misafir admin paketini indirmesin" argümanından **daha ağır** basar ve kararın
asıl dayanağıdır.

**İkincil nedenler:** SSR sunucusu yalnızca misafir tarafında çalışır (admin için SSR bir sunucu
daha demek ve orada token tutmak demektir — sıfır SEO faydası için genişleyen saldırı yüzeyi);
paket ayrımı sayesinde admin route/permission ağacı public bundle'a hiç girmez.

---

## 2. Angular workspace yapısı

```
src/frontend/
├── angular.json                  # newProjectRoot: "projects"
├── package.json                  # tek node_modules, tek npm script kümesi
├── tsconfig.json                 # paths: "@hotelcore/shared" -> projects/shared/src/public-api.ts
├── src/                          # proje adı: hotelcore-web (ADMIN) — YERİ DEĞİŞMEZ
│   └── app/{core,features,layout,shared}/
└── projects/
    ├── shared/                   # proje adı: shared → import: "@hotelcore/shared"
    │   ├── src/public-api.ts     # tek dışa açılım noktası (barrel)
    │   ├── src/{i18n,ui,formatting,http,a11y,public-api-types}/
    │   ├── styles/               # tema tokenları (@theme), ortak CSS
    │   └── assets/               # marka ikonları, favicon
    └── guest-web/                # proje adı: guest-web — MISAFIR uygulaması (SSR)
        ├── src/app/…
        ├── src/main.server.ts · src/server.ts     # outputMode: "server"
        └── prerender-routes.ts
```

> **Proje adları sözleşmedir:** `hotelcore-web` (admin), `guest-web` (misafir), `shared`
> (ortak katman); import yolu **`@hotelcore/shared`**. `ng build guest-web`, `ng lint shared`
> gibi komutlar ve CI adımları bu adlara dayanır — ajanlar kendi adlandırmasını uydurmaz.

> **`shared` bir ng-packagr paketi DEĞİLDİR** (mevcut karar, `projects/shared/src/public-api.ts`
> başlığında gerekçesiyle yazılı): kaynak, `tsconfig.json` → `paths` üzerinden doğrudan derlenir.
> Paket npm'e yayınlanmadığı için ayrı bir derleme hattının (partial compilation, dist bağlama,
> watch modunda ikinci build) maliyetinin karşılığı yoktur. `angular.json`'daki `shared` projesinin
> yalnızca `lint` target'ı vardır; **build target'ı eklenmez**.

**Admin uygulaması taşınmaz.** `projects/admin/` altına taşımak mimari olarak daha simetrik
olurdu ama yüzlerce import yolunu, `tsconfig` path'lerini, CI cache anahtarlarını ve test
yollarını kırar; kazanç yalnızca kozmetiktir. `angular.json`'da `newProjectRoot: "projects"`
zaten tanımlı olduğu için karışık düzen (root app + projects/*) desteklenir.

### 2.1 Paylaşılan kütüphanenin sınırı — kesin liste

**Kütüphaneye GİRER:**
- Tasarım tokenları / tema CSS (`@theme` blokları, "Otel Defteri" renk-tipografi ölçeği).
- Marka işareti bileşeni (`<hc-brand-mark>`) — marka adını **prop olarak alır**, hardcode etmez.
- i18n **sözleşmesi ve durumu**: `AppLanguage`, desteklenen diller, locale eşlemesi ve
  `LanguageStore`. Sözleşme ortak olmazsa panel `tr` derken site `tr-TR` der ve `Accept-Language`
  başlıkları ayrışır. **Mesaj katalogları girmez** (aşağı bkz.).
- Biçimlendirme: para/tarih/sayı locale yardımcıları (`de-DE: 1.234,50 €`), ISO tarih yardımcıları.
- HTTP ortak katmanı: `ProblemDetails` modeli + mapper, `Accept-Language` interceptor'ı,
  retry/timeout politikası.
- **Public API tipleri ve üretilmiş client** (`ng-openapi-gen` çıktısı, public OpenAPI belgesinden).
- Erişilebilirlik ilkelleri (focus-trap, skip-link, live-region duyurucu).

**Kütüphaneye GİRMEZ (sert kural):**
- **Yan etki / politika kodu.** Mevcut ayrım korunur: dilin **durumu** (`LanguageStore`) paylaşılır,
  dilin **nereden okunacağı** (`LanguageService`) paylaşılmaz — panelde `localStorage`, misafir
  sitesinde **URL dil öneki** (SEO gereği). Her uygulama kendi servisini yazar.
- Yerleşim/kabuk bileşenleri: panelin yoğun "defter" dili ile misafir tarafının fotoğraf/boşluk
  dili farklıdır; ortak bir "header" ikisine de kötü uyar.
- **JWT okuyan/yazan hiçbir şey.** Auth interceptor, token deposu, refresh akışı, permission
  guard'ları, hotel-switcher. *Gerekçe:* kütüphane misafir paketine giriyor; kimlik bilgisi
  yönetimi içeren bir modülün oraya girmesi, ayrı origin ile kurduğumuz sınırı anlamsız kılar.
  Bunu bir lint kuralı ve bir test korur (§8).
- Admin API client'ları (`*.api.ts`), admin domain modelleri, admin layout/sidebar.
- Mesaj katalogları (`assets/i18n/*.json`) — iki uygulama **ayrı** katalog taşır; ortak
  kütüphaneye konursa misafir, admin'in tüm ekran metinlerini indirir (hem boyut hem bilgi
  sızıntısı: iç modül adları).

### 2.2 SSR / prerender kararı

Misafir uygulaması `@angular/ssr` kullanır; **her route tek bir moda atanır** ve mod route
tanımında açıkça yazılır:

**Aşağıdaki tablo uygulanan hâlidir** (ilk taslaktaki Almanca segmentli ve `hotelSlug`'lı yollar
uygulanmadı; gerekçesi tablonun altında ve api-contracts-public-booking.md §13.2/F1'de).

| Route | Mod | Neden |
|---|---|---|
| `/{lang}` (ana sayfa) | **Prerender (SSG)** | İçerik dateless; CDN'den saniyeler içinde |
| `/{lang}/legal/imprint` · `/legal/privacy` · `/legal/terms` | **Prerender** | §5 DDG / Art. 13 sayfaları her zaman erişilebilir olmalı — **içerik derleme anında HTML'e gömülür** (§7.2) |
| `/{lang}/rooms/{code}` (oda tipi detay) | **SSR (istek anında)** | SEO'nun asıl hedefi, **ama fiyat ve müsaitlik canlıdır**: bir hafta önce üretilmiş sayfa yanlış fiyat gösterir (PAngV). Taslakta prerender yazıyordu; değiştirildi |
| `/{lang}/search?checkIn=…` (sonuçlar) | **SSR** | Tarihe bağlı; **asla cache'lenmez**, `noindex` |
| `/{lang}/booking` (form, özet, buton) | **CSR** (SSR devre dışı) | Kişisel veri taşır; sunucuda render edilmesi log/cache riskidir |
| `/{lang}/confirmation/{accessToken}` (onay) | **CSR** | Aynı; ayrıca `noindex, nofollow` (HTTP başlığı olarak da) |
| `/{lang}/manage` · `/{lang}/manage/{accessToken}` (sorgulama + iptal) | **CSR** | Aynı gerekçe; girilen e-posta sunucu log'una düşmemeli |

- **`hotelSlug` yolda değildir.** Bu tur **otel başına alan adı** dağıtımını hedefler: host → slug
  çevirisi dağıtım yapılandırmasındadır, uygulama slug'ı yapılandırmadan okur
  (`environment.hotelSlug` → `GUEST_HOTEL_SLUG` token'ı) ve **her API çağrısında yola koyar**.
  Çok otelli marka sitesi eklendiğinde tek değişiklik bu token'ın bir rota `resolve`'undan
  beslenmesidir; **API sözleşmesi değişmez.**
- Prerender kombinasyonu **dil × sayfa**'dır (12 sayfa): `/de|/en|/tr` + üç hukuki sayfa.
- Booking/confirmation sayfalarında `<meta name="robots" content="noindex,nofollow">` **zorunlu**.
- SSR sunucusu **hiçbir sır tutmaz**: public API anonimdir, sunucu tarafında token yoktur.
  Dağıtımda `SSR_ALLOWED_HOSTS` gerçek alan adlarını taşır (SSRF / mutlak adres zehirlenmesi).

### 2.3 Prerender'a içerik nasıl giriyor — §5 DDG'nin çalışma koşulu

Prerender **derleme anında** çalışır: gelen bir HTTP isteği yoktur, dolayısıyla göreli adresi
mutlaklaştıran interceptor devre dışı kalır ve `GET /legal` düşer. İlk hâlde sonuç şuydu: uretilen
`/{lang}/legal/imprint/index.html` **künye yerine hata paneli** içeriyordu — JavaScript
çalıştırmayan bir ziyaretçi Impressum'u hiç göremiyordu. §5 DDG "unmittelbar erreichbar" istediği
için bu bir eksik değil, bir **uygunsuzluktur**.

**Karar: derleme öncesi anlık görüntü (snapshot), derleme anında canlı API değil.**

```bash
cd src/frontend
GUEST_API_TARGET=http://localhost:5080 npm run legal:snapshot   # uc dili birden ceker
npm run legal:snapshot:check                                     # ag gerektirmez; CI bunu kosar
```

- Çıktı `projects/guest-web/src/generated/legal-snapshot.json` dosyasıdır ve **depoda durur**.
  Prerender sırasında `legalPrerenderInterceptor` `GET /legal` yanıtını bu dosyadan verir; SSR'da
  ve tarayıcıda **hiçbir şey yapmaz**, yani canlı metin her zaman kazanır.
- **Neden canlı API değil:** frontend derlemesi ayakta bir backend + veritabanı istemez; CI'a
  servis, secret ve sıra bağımlılığı eklenmez; aynı commit her yerde aynı HTML'i üretir. Ayrıca
  bir Impressum değişikliği **gözden geçirilebilir bir diff** olarak görünür — derleme anında
  çekilen içerikte böyle bir iz kalmaz.
- **Bedeli ve nasıl karşılandığı:** metin değişip anlık görüntü tazelenmezse prerender edilmiş
  sayfa eskir. (a) Tarayıcı hidrasyondan sonra canlı veriyi çeker ve içerik güncellenir,
  (b) anlık görüntü belgelerin `version` alanını taşır, (c) CI üretilen HTML'de künyeyi **arar**:
  boş bir hukuki sayfa iş akışını kırar.
- Anlık görüntü **otel başınadır** (`GUEST_HOTEL_SLUG`); misafir uygulaması zaten tek otele
  dağıtıldığı için bu, dağıtım birimiyle aynı sınırdır.

---

## 3. Public API'nin yeri: aynı uygulama, ayrı yüzey

**Karar:** public uçlar **aynı ASP.NET Core uygulamasında**, `/api/v1/public/...` önekiyle yaşar.

*Neden ayrı bir servis/proje değil:* fiyat ve müsaitlik mantığı tek kaynaktan gelmek zorunda
(verilmiş karar 4). Ayrı bir servis, ya kodu kopyalamayı ya da bir iç API çağrısı katmanı
eklemeyi gerektirirdi; ikisi de "iki taraf birbirini tutmaz" riskini geri getirir.

*Neden ayrı bir sürüm (`/api/public/v1`) değil:* sürüm numarası backend'in **sözleşme evrimini**
ifade eder, kitleyi değil. İki ayrı sürüm ekseni, aynı domain değişikliğinin iki kez
versiyonlanmasını gerektirirdi.

**Ama OpenAPI belgesi ikiye ayrılır:**

| Belge | Yol | İçerik |
|---|---|---|
| Admin | `/swagger/v1/swagger.json` | Yalnızca `/api/v1/**` (public **hariç**) |
| Public | `/swagger/public-v1/swagger.json` | Yalnızca `/api/v1/public/**` |

Böylece misafir uygulaması client'ını public belgeden üretir ve admin şemalarının **tek bir
tipini bile** görmez. Belge ayrımı `ApiExplorer` grup adıyla (`GroupName = "public"`) yapılır.

---

## 4. Kimlik ve çok kiracılılık — anonim istekte `HotelId` nasıl kurulur

Bu, projedeki **en riskli** noktadır: `AppDbContext` global query filter'ı `ICurrentUser.HotelId`
okur; kimlik yoksa `null` döner ve *hiçbir tenant satırı görünmez* (güvenli varsayılan). Public
uçların çalışması için filtreye anonim ama **kesin** bir otel verilmelidir.

### 4.1 Otel nasıl belirlenir: **URL yolundaki `hotelSlug`**

Her public yol otelin slug'ını taşır: `/api/v1/public/hotels/{hotelSlug}/...`

| Aday | Karar | Gerekçe |
|---|---|---|
| **Yol parametresi (`hotelSlug`)** | **SEÇİLDİ** | URL SEO'nun ve CDN cache anahtarının **kendisidir**. Prerender edilmiş sayfalar otel başına ayrı URL ister. Token taşıyan uçlarda bile slug yolda durur (§4.3). |
| `X-Hotel-Id` header | **Elendi** | Header CDN cache anahtarına girmez → tüm oteller aynı URL'de görünür ve yanlış otelin sayfası cache'ten servis edilebilir. Crawler header göndermez. Ayrıca GUID'i public URL'e/isteğe taşımak iç kimlikleri dışarı verir. |
| Alan adı (Host) | **Kısmen** | Birincil mekanizma değil: her otel için DNS + TLS sağlanmadan hiçbir şey çalışmaz, yerelde test edilemez. **Opsiyonel katman** olarak kalır: `Hotel.PublicHost` doluysa edge/SSR katmanı host → slug çevirir; **API yine slug alır**. Sözleşme değişmez. |

`Hotel.PublicSlug`: küçük harf, `a-z0-9-`, 3–60 karakter, **canlı satırlar arasında global
benzersiz** (partial unique index, `NOT IsDeleted` — architecture.md §10 kuralı). Global,
head-office bazında değil: URL uzayı globaldir.

`HeadOffice.PublicSlug`: marka sitesi ve otel listesi için (`/api/v1/public/brands/{brandSlug}/hotels`).

### 4.2 Tenant kapsamı: `ITenantContext`

`AppDbContext` artık `ICurrentUser` yerine **`ITenantContext`** okur:

```csharp
public interface ITenantContext
{
    Guid? HotelId { get; }
    bool CanAccessAllHotels { get; }
    TenantScopeSource Source { get; }   // None | Authenticated | PublicChannel
}
```

- `Authenticated` → mevcut `CurrentUser` davranışı **birebir korunur** (X-Hotel-Id, allHotels,
  konsolide mod). Davranış değişikliği yoktur.
- `PublicChannel` → `PublicTenantMiddleware` yoldaki slug'ı çözer:
  - slug bulunamazsa **veya** `Hotel.PublicBooking.IsEnabled == false` ise → **404**, handler hiç
    çalışmaz (403 değil: otelin varlığı sızdırılmaz — architecture.md §10'daki mevcut kuralla aynı).
  - bulunursa `HotelId = <o otel>`, `CanAccessAllHotels = **false**`.

**Değişmez (invariant), testle korunur:**
`Source == PublicChannel ⇒ HotelId != null && CanAccessAllHotels == false`.
Bu iki koşuldan biri bozulursa public bir istek ya hiçbir şey görür ya **her şeyi** görür; ikisi
de kabul edilemez, bu yüzden kural bir sözleşme testidir, yorum değil.

**Ek kurallar:**
- Public route'ta `Authorization` header'ı **tamamen yok sayılır** (endpoint `[AllowAnonymous]` ve
  middleware kimliği okumaz). "Admin token + public uç = daha geniş veri" yolu hiç açılmaz.
- Public route'ta `X-Hotel-Id` header'ı **yok sayılır** (400 değil, sessizce yok sayılır — public
  istemci bunu göndermez; gönderirse otoritenin yol parametresi olduğu değişmez).
- **`IgnoreQueryFilters()` public yolda YASAKTIR.** Token ile erişilen uçlarda bile slug yolda
  olduğu için filtre önce kurulur, sorgu sonra çalışır. Bir otelin token'ı başka otelin yolunda
  sunulursa satır filtreye takılır ve **404** döner — ayrı bir kontrol yazmaya gerek kalmaz.
  (Bu, "önce token'ı filtresiz bul, otelini öğren, sonra kapsamı kur" tasarımına göre bilinçli
  tercihtir; o tasarım public yola tek bir filtre bypass'ı sokardı.)

### 4.3 Public DTO'lar admin DTO'larından **ayrıdır**

Tüm public yanıt tipleri `Application/Features/Public/**` altında, `Public` önekiyle yaşar
(`PublicRoomTypeResponse`, `PublicPriceResponse`, …) ve **hiçbir admin DTO'sunu kullanmaz,
miras almaz, sarmalamaz.**

*Gerekçe (paylaşmanın somut zararı):* admin DTO'ları zamanla büyür — `RoomTypeResponse`'a yarın
maliyet, doluluk veya iç not eklenir. Paylaşılan bir tip, o alanı **sessizce** public yanıta
taşır; kimse bir güvenlik kararı vermediği hâlde veri sızar. Ayrılık, sızıntıyı bir *unutma*
hatasından bir *bilinçli ekleme* hatasına dönüştürür.

**Public yanıtta bulunması YASAK alanlar (tam liste):**
oda numarası · kat · `housekeepingStatus` · `isOutOfOrder` · oda/oda tipi iç notu · `roomId` ·
`roomTypeId` (GUID) · başka misafirlerin adı/e-postası · rezervasyon sayıları, doluluk oranı,
ADR/RevPAR · maliyet · fatura/folio verisi · `reservationNumber` (`RES-…`) · `ratePlanId`/plan adı ·
`Reservation.Notes` (iç not) · personel bilgisi · `HotelId`/`HeadOfficeId` GUID'leri.

Kimlikler public tarafta **GUID değil, stabil metin anahtarlarıdır**: otel → `hotelSlug`,
oda tipi → `roomTypeCode`, rezervasyon → `bookingReference` (§6.3).

---

## 5. Müsaitlik semantiği ve yarış koşulu

### 5.1 Oda tipi bazında satış, oda ataması sunucuda

- **Arama, teklif ve fiyat oda tipi bazındadır.** Misafir kategori satın alır; bu hem sektör
  normudur hem de oda numarası/kat listelemek otelin yerleşim planını ve doluluğunu ifşa eder.
- `Reservation.RoomId` **zorunlu** (mevcut şema). Bu yüzden somut oda **hold anında** sunucuda
  atanır. Seçim **deterministiktir**: uygun odalar arasında `floor` sonra `number` (doğal sıra)
  ilk sıradaki. Rastgele seçim testi imkânsızlaştırırdı.
- Resepsiyon sonradan aynı tipte başka odaya taşıyabilir (mevcut `PUT /reservations/{id}`);
  public taraf bundan etkilenmez, çünkü misafire oda numarası hiç gösterilmez.
- Bir oda tipi verilen aralıkta müsaittir ⇔ o tipte, **tüm gecelerde** boş **ve** aktif bir
  hold'u olmayan **en az bir** oda vardır. Boşluk tanımı mevcut `AvailabilityQuery.BlockingBetween`
  ile **aynıdır** (`[checkIn, checkOut)`, `Cancelled`/`NoShow` bloke etmez, `IsOutOfOrder` müsait değil).

### 5.2 Aynı anda iki misafir son odayı isterse: **hold + veritabanı kısıtı (ikisi birden)**

**Karar: 15 dakikalık geçici tutma (hold) VARDIR.**

Neden gerekli — üç bağımsız sebep:
1. **§312j Abs. 2 BGB:** sipariş düğmesinin hemen üstündeki zorunlu özet, **gerçekten
   ödenecek** toplam fiyatı göstermek zorundadır. Fiyat/müsaitlik özet ile buton arasında
   değişirse gösterilen özet yanlıştır. Hold, bu pencereyi kapatır.
2. **Sözleşme kurulumu:** misafir "zahlungspflichtig buchen"e bastıktan *sonra* "son oda satıldı"
   demek, tüketicinin iradesini beyan ettiği anda sözleşmenin kurulmaması demektir — hem hukuken
   tartışmalı hem ürün olarak en kötü an.
3. **Oda ataması:** somut oda seçimi hold'da donar; aksi hâlde iki eşzamanlı istek aynı odayı seçer.

**Süre 15 dakika.** Form doldurma süresi (ölçülen sektör ortalaması 4–6 dk) ile bot'ların envanteri
park etme maliyeti arasındaki denge. Uzatma **yoktur**; misafir geri gidip yeniden teklif alırsa
yeni bir hold oluşur (oda hâlâ boşsa aynı oda seçilir). Süre otel bazında ayarlanabilir değildir —
tek bir davranış, tek bir test.

**Ama hold TEK BAŞINA yetmez.** Kalan yarış pencereleri: hold tam süresi dolarken gelen istek,
resepsiyonun aynı odayı elle satması, sweeper gecikmesi. Bu yüzden **ikinci katman** eklenir:

```sql
-- Reservations üzerinde ÇİFT REZERVASYON kısıtı (bugün YOK — mevcut açık)
ALTER TABLE "Reservations"
  ADD CONSTRAINT "EX_Reservations_NoOverlappingStays"
  EXCLUDE USING gist (
      "RoomId" WITH =,
      daterange("CheckIn", "CheckOut", '[)') WITH &&
  ) WHERE ("Status" NOT IN ('Cancelled','NoShow') AND NOT "IsDeleted");
```

- `'[)'` **yarı açık** — mevcut `[CheckIn, CheckOut)` semantiğiyle birebir aynı; çıkış günü ertesi
  girişi engellemez.
- Predikat yalnızca **immutable** ifadeler içerir (enum string olarak saklandığı için `NOT IN`
  metin karşılaştırmasıdır; `now()` gibi bir ifade partial index predikatında **kullanılamaz**).
- İhlal SQLSTATE **23P01** üretir; `AppDbContext` bunu zaten **409**'a çeviriyor (mevcut kod),
  yani ek bir çeviri yazılmaz.
- **Bu kısıt admin tarafını da düzeltir:** bugün iki eşzamanlı resepsiyon isteği ön kontrolü
  birlikte geçip aynı odayı iki kez satabiliyor (`AvailabilityService` kilit almıyor). Public kanal
  bu riski trafiğiyle görünür kılacaktı; kısıt onu kaynağında kapatır.

**`BookingHold` için de bir kısıt:**
```sql
ALTER TABLE "BookingHolds"
  ADD CONSTRAINT "EX_BookingHolds_NoOverlappingActiveHolds"
  EXCLUDE USING gist (
      "RoomId" WITH =,
      daterange("CheckIn", "CheckOut", '[)') WITH &&
  ) WHERE ("ConsumedAt" IS NULL);
```
Süresi dolmuş hold **fiziksel olarak silinir** (kısıt predikatına zaman ifadesi konamayacağı için):
hold oluşturma handler'ı aynı transaction'da ilgili oda tipi + kesişen aralık için süresi dolmuş
hold'ları siler; ayrıca bir `HostedService` her 5 dakikada bir `ExpiresAt < now() - 1h` olanları
ve `ConsumedAt < now() - 24h` olanları süpürür.

### 5.3 Elenen alternatifler

| Alternatif | Neden elendi |
|---|---|
| **Hold yok, yarışı `EXCLUDE` çözsün, kullanıcıya 409 göster** | §312j özeti yanlış fiyat/uygunluk gösterebilir; hata en kötü anda (tüm veri girildikten, butona basıldıktan sonra) gelir. `EXCLUDE` yine de **kalır**, ama *son güvence* olarak, tek mekanizma olarak değil. |
| **Hold = `Reservation.Status = Option`** | `Option` ticari ve **operasyonel** bir durumdur: rezervasyon numarası tüketir, doluluk grid'inde görünür, raporlara ve folio'ya girer, `Guest` kaydı ister. Terk edilmiş sepetler resepsiyonun takvimini ve `Guest` tablosunu kirletir; ayrıca DSGVO açısından henüz gerekmeyen kişisel veriyi erkenden yaratır. |
| **Sayaç/kontenjan (allotment) bazlı hold, oda pinlenmeden** | `Reservation.RoomId` zorunlu; ayrıca sayaç `EXCLUDE` ile korunamaz, kendi kilitleme şemasını ister — iki farklı eşzamanlılık modeli. |
| **`SELECT … FOR UPDATE` ile oda kilidi** | Kilit süresi HTTP isteğine bağlanır; misafirin form doldurduğu 5 dakika boyunca DB satırı kilitli tutulamaz. |

### 5.4 Kullanıcıya ne gösterilir

- Arama sonucunda **kalan oda sayısı ham hâliyle verilmez** (doluluk ifşası). Yanıt
  `availableUnits` alanını **5'te kırpar** (`availableUnitsCapped: true` ise "5+" demektir).
  Kırpılmış değer **gerçektir** — UWG §5 (yanıltıcı reklam) gereği "son 2 oda" iddiası doğru
  olmak zorundadır; kırpma doğruluğu bozmaz, yalnızca üst sınırı gizler.
- Hold süresi dolduysa: sepet ekranında **409 `HOLD_EXPIRED`** → "süre doldu, fiyat ve uygunluk
  yenilendi" ve akış otomatik olarak yeni teklife döner (yeni fiyat açıkça gösterilir).
- Hold geçerliyken oda gerçekten kaybolduysa (kısıt ihlali): **409 `ROOM_NO_LONGER_AVAILABLE`**,
  aynı tipte başka oda varsa şeffaf biçimde yeniden atanır, yoksa arama sonuçlarına dönülür.

### 5.5 Hold **yönetim paneline karşı görünmezdir** — ölçülmüş davranış

Uçtan uca doğrulamada bilerek denendi ve sonuç şudur:

| Adım | Sonuç |
|---|---|
| Misafir `POST /holds` alır (oda 202 pinlenir) | Misafir tarafında `availableUnits` **5 → 4** |
| Yönetim panelinde `GET /availability` (aynı tarihler) | **5 oda müsait**, 202 dâhil — hold **görünmez** |
| Resepsiyon 202'yi aynı tarihe satar (`POST /reservations`) | **201 Created.** Veritabanı engellemez: iki `EXCLUDE` kısıtı **farklı tablolardadır** |
| Misafir hold'unu rezervasyona çevirir | **409 `ROOM_NO_LONGER_AVAILABLE`** (`EX_Reservations_NoOverlappingStays`) |

Yani **hold yalnızca misafir kanalı içinde bağlayıcıdır**; admin yazmalarına karşı tavsiye
niteliğindedir ve çakışmayı nihai olarak `Reservations` kısıtı çözer. Kaybeden taraf misafirdir.

Bu bilinçli bir denge: resepsiyonun ekranını bir web sepeti yüzünden bloke etmek (ve "kimin
tuttuğu belli olmayan" odalar göstermek) operasyonel olarak daha kötüdür. Kabul edilebilir olmasının
koşulu, pencerenin **15 dakika** olması ve misafire gösterilen hatanın "oda kalmadı" (bir mekanizma
açıklaması değil) olmasıdır. Değişmesi istenirse tek yol, hold'u yönetim panelinin müsaitlik
sorgusuna da dâhil etmek ve orada "geçici olarak tutuluyor" göstermektir — o zaman resepsiyon
bilinçli olarak üzerine yazmayı seçebilir.

---

## 6. Ödeme, kart verisi ve PCI-DSS

### 6.1 Karar: "girişte ödeme" + opsiyonel kart garantisi, PSP **soyutlamanın arkasında**

```csharp
public interface IPaymentAuthorizationProvider
{
    string Key { get; }                       // "none" | "stripe" | "adyen" | …
    bool SupportsGuarantee { get; }
    Task<GuaranteeAuthorization> AuthorizeAsync(GuaranteeRequest request, CancellationToken ct);
    Task VoidAsync(string providerReference, CancellationToken ct);
}
```
Varsayılan kayıt: **`NullPaymentProvider`** (`SupportsGuarantee == false`). Bu fazda:
- `paymentOptions` yalnızca `{ "method": "PayAtProperty", "requiresGuarantee": false }` döner.
- İstemci `guarantee: "CardGuarantee"` isterse → **400 `CHANNEL_NOT_CONFIGURED`**. Sessizce
  yok sayılmaz; sözleşme yalan söylemez.
- PSP takıldığında yalnızca bu arayüzün implementasyonu ve `paymentOptions` içeriği değişir;
  **DTO'lar ve uç yolları değişmez**.

### 6.2 Kart verisi: mutlak yasak, tripwire ile

- Public sözleşmede **hiçbir uçta** `pan`, `cardNumber`, `cvc`, `cvv`, `expiryMonth`,
  `expiryYear`, `cardholderName` alanı **yoktur ve olmayacaktır**.
- **Tripwire:** public POST gövdesinde bu adlardan biri geçerse istek **400
  `CARD_DATA_NOT_ACCEPTED`** ile reddedilir ve gövde **loglanmaz**. Amaç, iyi niyetli bir
  geliştiricinin "geçici olarak" kart alanı eklemesini imkânsız kılmaktır.
- Kart yalnızca **PSP'nin kendi iframe/SDK'sı** ile alınır; bize dönen tek şey opaque bir
  `providerReference`'tır (token). Bu değer bir ödeme aracı değildir, tek başına para çekmez.
- **PCI-DSS gerekçesi:** kart verisi sistemlerimize *hiç* girmezse kapsam dışı kalırız (SAQ-A
  sınıfı). Bir kez bile PAN kabul etmek, tüm API'yi, tüm log altyapısını, tüm yedekleri ve tüm
  geliştirme ortamlarını kapsama sokar. Bu, geri dönüşü çok pahalı bir eşiktir.
- Public uçların istek gövdeleri **hiçbir log seviyesinde tam olarak yazılmaz**; hata loglarında
  yalnızca alan **adları** ve doğrulama anahtarları görünür.

---

## 7. Şema değişiklikleri (Database Agent'ın kapsamı)

| # | Değişiklik | Not |
|---|---|---|
| 1 | `Hotel.PublicSlug` (string, 60) | Partial unique (`NOT IsDeleted`), global |
| 2 | `Hotel.PublicHost` (string?, 253) | Opsiyonel; edge host→slug eşlemesi |
| 3 | `Hotel.TimeZoneId` (string, IANA, varsayılan `Europe/Berlin`) | İptal son tarihi ve yerel gün hesabı için **zorunlu**. README §14'teki "otelde saat dilimi yok" eksiğini kapatır |
| 4 | `Hotel.CheckInFromLocal`, `CheckOutUntilLocal` (TimeOnly) | §312j "süre" bilgisinin parçası |
| 5 | `Hotel.VatId` (USt-IdNr., ayrı kolon) | Mevcut `TaxNumber` **Steuernummer** olarak kalır. §5 DDG Impressum USt-IdNr. ister; README §14'teki "ayrılmıyor" eksiğini kapatır |
| 6 | `PublicBookingSettings` (owned, Hotel üzerinde) | `IsEnabled`, `MinNights`, `MaxNights`, `MaxAdvanceDays`, `MinAdvanceHours`, `ConfirmationMode` |
| 7 | `CancellationPolicy` (owned, Hotel üzerinde) | `FreeCancellationDaysBeforeArrival`, `CutoffLocalTime`, `LateCancellationFeePercent`, `NoShowFeePercent` |
| 8 | `HotelLegalProfile` (owned, Hotel üzerinde) | Impressum alanları (§9.4) |
| 9 | `HeadOffice.PublicSlug` | Marka sitesi |
| 10 | `RoomTypeImage`, `HotelImage` (entity) | `Url`, `SortOrder`, `Alt` (çeviri tablosu üzerinden). **Bu fazda yalnızca URL saklanır; yükleme/CDN pipeline'ı yok** |
| 11 | `BookingHold` (entity, `ITenantEntity`) | §5.2; `TokenHash` (SHA-256, unique), donmuş teklif alanları |
| 12 | `PublicBooking` (entity, `ITenantEntity`, `Reservation` ile 1:1) | Public referans, erişim token hash'i, **rıza ve hukuki anlık görüntü** (§9.7) |
| 13 | `ReservationChannel.Website = 7` | Aşağı bkz. — **davranış etkisi var** |
| 14 | `EX_Reservations_NoOverlappingStays` | §5.2 — mevcut açığı kapatır |
| 15 | `EX_BookingHolds_NoOverlappingActiveHolds` | §5.2 |

> **`Guest` şeması değişmez.** Public rezervasyon `Guest`'i mevcut alanlarıyla, çoğu `null`
> bırakarak oluşturur (§9.6 veri minimizasyonu).

### 7.1 `ReservationChannel.Website` — dikkat gerektiren yan etki

Yeni kanal eklenir çünkü web satışını `Direct`(doğrudan/telefon dışı) ile karıştırmak kanal
dağılımı raporunu ve komisyon analizini anlamsız kılar. Enum **string** saklandığı için veri
migration'ı gerekmez.

**Ama:** `RatePlan.Channel == Direct` olan mevcut planlar **web rezervasyonlarına uygulanmaz**
(fiyat seçimi kanalı birebir karşılaştırır — `ReservationPricingService`). Sonuç: bugün
`Direct` planı olan bir otelde web fiyatı "tüm kanallar" planına, o da yoksa `BasePrice`'a düşer.
**Bu, otel ayarında açıkça duyurulmalıdır:** public kanal açılırken UI, web için geçerli bir plan
bulunup bulunmadığını gösterir ve yoksa uyarır. Seed verisi web için `Channel: null` (tüm kanallar)
bir plan içerecek şekilde güncellenir.

---

## 8. Fiyatın tek kaynağı — kopyalama yasağı

Public teklif, admin tarafının **aynı** kodunu çağırır:

| Hesap | Tek sahibi | Public tarafta yapılacak |
|---|---|---|
| Gece gece konaklama fiyatı, sezon geçişi, kanal önceliği | `ReservationPricingService` | **Yeniden kullanılır.** Mevcut `CalculateAsync(roomId, …)` ikiye bölünür: `CalculateForRoomTypeAsync(roomTypeId, …)` asıl hesabı yapar, `CalculateAsync(roomId, …)` odadan tipi çözüp ona **delege eder**. İki dal, tek hesap. |
| Net/KDV ayrıştırma, yuvarlama, `net + vat == gross` | `InvoiceAmounts` | **Yeniden kullanılır** (`ComputeLine` / `ApplyLineAmountsFromGross` ile aynı matematik) |
| KDV oranı eşlemesi (konaklama = indirimli) | `InvoiceAmounts.ResolveVatRate` | **Yeniden kullanılır** |
| Kurtaxe'ye tabi kişi sayısı | `TaxProfile.CountTaxablePersons` | **Yeniden kullanılır** (çocuk muafiyeti dâhil) |
| Kurtaxe doğar mı | `CityTaxLiability.ArisesFrom` | **Yeniden kullanılır** (iptal/no-show'da Kurtaxe yok) |
| Çakışma / bloke eden durumlar | `AvailabilityQuery` | **Yeniden kullanılır** |

**Zorunlu test (DevOps Agent):** aynı otel, aynı tarih, aynı oda tipi için
`GET /public/.../availability` toplam brüt tutarı ile o rezervasyondan üretilen faturanın
`grossAmount`'u **kuruşu kuruşuna eşit** olmalıdır (Kurtaxe dâhil). Bu test, ikinci bir fiyat
motorunun sessizce doğmasını kalıcı olarak engeller.

---

## 9. Alman mevzuatı — hangi kural hangi uç/alan/ekranla karşılanır

> Ayrıntılı alan adları ve JSON'lar sözleşme dosyasındadır. Burada **eşleme** ve **gerekçe** vardır.

### 9.1 §312j Abs. 3 BGB — Button-Lösung
- **Ekran:** rezervasyon özeti sayfası, sipariş düğmesi.
- **Metin:** DE'de **tam olarak** `zahlungspflichtig buchen`. Metin bir i18n anahtarıdır
  (`legal.orderButton.payable`) ve **otel bazında değiştirilemez** — sözleşme
  `orderButton.mustBeExactLabel: true` ile bunu bildirir.
- **Sunucu ne yapabilir:** buton metnini sunucu göremez. Yapılabilecek olan **kanıt saklamaktır**:
  istemci `checkout.orderButtonLabel` alanında gösterdiği metni gönderir; sunucu bunu
  `PublicBooking` içine **dondurur**. Uyuşmazlıkta otelin elinde ne gösterildiğinin kaydı olur.
  Sunucu metni doğrulamaz (dil/varyant meşru olabilir), **kaydeder**.
- Ödeme otelde yapılsa da düğme gereklidir: §312j ücretli sözleşmelere uygulanır, ödemenin
  *zamanı* önemli değildir. → **Hukuki onay gerekir** (§10, madde 1).

### 9.2 §312j Abs. 2 BGB — düğmenin hemen üstündeki zorunlu özet
- **Uç:** `POST /public/hotels/{slug}/holds` yanıtındaki **`orderSummary`** nesnesi.
- Nesne üç zorunlu bileşeni **yapısal olarak** taşır: `essentialFeatures` (oda tipi, kişi sayısı,
  pansiyon), `duration` (giriş/çıkış/gece + yerel saatler), `totalPrice` (KDV dâhil, tüm zorunlu
  kalemler dâhil). Düz metin değil, **alan alan** verilir → frontend bir kalemi "unutamaz".
- `orderSummary.hash` = sunucunun kanonik JSON üzerinden hesapladığı SHA-256. İstemci bunu
  `POST /bookings` içinde geri gönderir; uyuşmazsa **409 `SUMMARY_CHANGED`**. §312j Abs. 2'nin
  makine ile zorlanabilir kısmı budur.

### 9.3 PAngV — KDV dâhil toplam fiyat ve zorunlu ek kalemler
- **Uç:** her fiyat taşıyan yanıttaki `price` nesnesi.
- `price.totalGross` = `accommodationGross + cityTax.amount`. **Kurtaxe toplama dâhildir**
  (PAngV: Gesamtpreis tüm zorunlu bileşenleri içerir). `vatIncluded: true` ve
  `mandatoryExtrasIncluded: true` alanları bunu açıkça beyan eder.
- Kırılım faturayla **uzlaşır**: `accommodationNet + accommodationVat == accommodationGross`,
  `cityTax.vatRate == 0`. Kurtaxe faturada `NetAmount`'a girmez, ama **gösterilen toplama girer** —
  bu iki farklı sorunun iki farklı doğru cevabıdır (biri KDV matrahı, diğeri tüketici fiyatı).
- `nightly[]` gece gece fiyat verir; sezon geçişinde "gecelik X €" ifadesi tek bir sayıya
  indirgenemez, ortalama `averageNightlyGross` olarak **ayrı** alandadır ve etiketlenmelidir.

### 9.4 §5 DDG (Impressum) ve DSGVO Art. 13
- **Uç:** `GET /public/hotels/{slug}/legal`.
- Impressum alanları veritabanından gelir (`HotelLegalProfile`): `legalEntityName`, `legalForm`,
  `representedBy`, `addressLine`, `postalCode`, `city`, `country`, `phone`, `email`,
  `registerCourt`, `registerNumber` (HRB), `vatId` (USt-IdNr.), `supervisoryAuthority`,
  `disputeResolutionNotice` (ODR/VSBG bildirimi).
- **Hiçbiri hardcode edilmez** — marka adı gibi bunlar da müşteri-değişkenidir.
- Art. 13 aydınlatma metni ve versiyonu (`privacyNotice.version`) aynı uçtan döner; rezervasyon
  isteğinde **hangi versiyonun onaylandığı** kaydedilir (Art. 7 Abs. 1 hesap verebilirlik).
- **Ekran:** her sayfanın altbilgisinde "Impressum" ve "Datenschutz" bağlantısı; prerender edilir,
  yani JS kapalıyken de erişilebilir (§5 DDG "leicht erkennbar, unmittelbar erreichbar").

### 9.5 §25 TDDDG — çerez/izleyici onayı
- **API hiçbir çerez koymaz.** Public uçlar tamamen durumsuzdur; hold ve booking token'ları
  yanıt **gövdesinde** döner.
- İstemci tarafında yalnızca **kesinlikle gerekli** depolama kullanılır: aktif `holdToken` ve dil
  tercihi `sessionStorage`/`localStorage`'da. §25 Abs. 2 Nr. 2 istisnası: kullanıcının açıkça
  talep ettiği hizmetin (rezervasyonun) sunulabilmesi için zorunludur. Bu gerekçe gizlilik
  metninde **yazılı** olarak durur.
- **Analitik, ısı haritası, pazarlama pikseli, harici font, harici harita: onaysız YÜKLENMEZ.**
  Onay yönetimi tamamen istemci tarafındadır (API'de "consent" ucu yoktur); üçüncü taraf
  script'ler onay sinyali gelene kadar **DOM'a hiç eklenmez** (yalnızca gizlemek yetmez).
- Rezervasyon isteğindeki `consents` bloğu **çerez onayı değildir**; sözleşmesel onaylardır
  (AGB, aydınlatma, cayma bildirimi, pazarlama izni). İkisi karıştırılmaz.

### 9.6 DSGVO veri minimizasyonu — rezervasyonda ne sorulur, ne sorulmaz

| Alan | Rezervasyonda | Gerekçe |
|---|---|---|
| Ad, soyad | **Zorunlu** | Sözleşme tarafı (Art. 6 Abs. 1 lit. b) |
| E-posta | **Zorunlu** | §312f onayının kalıcı veri taşıyıcısı; iptal bağlantısı |
| Telefon | Opsiyonel | Geç geliş/kesinti iletişimi; zorunlu değil |
| Fatura adresi (şirket, adres, USt-IdNr.) | **Opsiyonel blok** | Yalnızca kurumsal fatura isteyene. §14 UStG alıcı künyesi ancak fatura istendiğinde gerekir; §33 UStDV küçük tutarlı faturada zaten aranmaz |
| Tahmini geliş saati | Opsiyonel | Operasyon kolaylığı |
| Misafir notu | Opsiyonel, ≤ 500 | Serbest metin; özel kategori veri istenmemesi için alan etiketinde uyarı |
| **Doğum tarihi, uyrukluk, kimlik/pasaport no, tam ev adresi, imza** | **SORULMAZ** | Bunlar **Meldeschein** verisidir (BMG §§29–30) ve **girişte** alınır. Rezervasyon anında toplamak amaç sınırlaması ve veri minimizasyonuna aykırıdır; ayrıca henüz gerçekleşmemiş bir konaklama için kimlik verisi saklamak gereksiz risktir |
| Ödeme kartı | **ASLA** | §6.2 |

- `Guest` kaydı **her rezervasyonda yeni açılır**; e-postaya göre mevcut kayıtla **birleştirilmez**.
  *Gerekçe:* birleştirme, e-postayı bilen herkesin başka birinin konaklama geçmişine bağlanmasına
  ve yanlış kişiye konaklama yazılmasına yol açar (mevcut sözleşmedeki "misafirde benzersizlik
  kuralı yoktur" kararıyla da aynı yöndedir).
- `Guest` tenant-scoped'dur: aynı kişi grubun iki otelini ayrı ayrı rezerve ederse iki kayıt oluşur.
  Bu, izolasyon açısından **istenen** davranıştır.

### 9.7 §312g Abs. 2 Nr. 9 BGB — cayma hakkı YOK, ama bildirilmeli
- **Uç/alan:** `legal.withdrawalRight` → `{ applies: false, legalBasis: "BGB §312g Abs. 2 Nr. 9",
  noticeKey: "legal.withdrawal.excluded.accommodation", noticeVersion: "…" }`.
- **Ekran:** özet sayfasında, düğmenin görüş alanında, **ayrı ve okunur** bir kutu. Genel bir
  "Widerrufsbelehrung" **gösterilmez** — var olmayan bir hakkı anlatmak yanıltıcıdır.
- Metin, tarihli konaklama istisnasını ve iptal politikasının **ayrı** bir şey olduğunu söyler
  (misafirin "iptal edemiyorum" sanmaması için: sözleşmesel iptal hakkı vardır, yasal cayma yoktur).
- Onay `consents.withdrawalNoticeAcknowledged` + versiyon olarak **dondurulur**.

### 9.8 §312f BGB — kalıcı veri taşıyıcısında onay
- **Mekanizma:** `IBookingConfirmationSender` (e-posta). Rezervasyonla **aynı transaction'da
  gönderilmez**; kayıt commit edilir, ardından outbox üzerinden gönderilir (gönderim hatası
  rezervasyonu iptal etmemelidir).
- **Zorunlu içerik** (e-postanın **gövdesinde**, yalnızca bağlantı olarak değil): otelin künyesi,
  `bookingReference`, oda tipi ve kişi sayısı, giriş/çıkış tarihleri ve yerel saatler, **KDV dâhil
  toplam fiyat ve kırılımı (Kurtaxe ayrı satır)**, ödeme şekli (girişte), iptal politikası ve
  **mutlak** ücretsiz iptal son tarihi, cayma hakkının bulunmadığı bildirimi, AGB metni/versiyonu,
  iptal bağlantısı (`accessToken`).
- `PublicBooking.ConfirmationSentAt`, `ConfirmationDocumentHash`, `ConfirmationCulture` saklanır.
- **Bağlantı yeterli mi?** İçeriğin gövdede olması tercih edilir; yalnızca bağlantı vermek
  tartışmalıdır → **hukuki onay** (§10, madde 2).

### 9.9 Kurtaxe'nin public gösterimi — `CityTaxLiability` ile tutarlılık
- Teklifte Kurtaxe **toplama dâhildir** (PAngV) ama **ayrı bir bileşen** olarak gösterilir ve
  `chargedOnlyIfStayTakesPlace: true` bayrağını taşır.
- İptal/no-show hâlinde **Kurtaxe tahsil edilmez** — mevcut `CityTaxLiability.ArisesFrom` kuralının
  birebir aynısı. Bu yüzden iptal ücreti **yalnızca konaklama tutarı** üzerinden hesaplanır;
  sözleşme bunu `cancellation.cityTaxRefundedOnCancellation: true` ile açıkça söyler.
- Çocuk muafiyeti açıksa `cityTax.taxablePersons` yetişkin sayısıdır ve teklif metni muafiyeti
  belirtir (fatura satırındaki açıklamayla aynı gerekçe: muafiyetin dayanağı belgede görünmeli).

---

## 10. İnsan onayı gereken hukuki/mali noktalar

> README'nin "Canlıya çıkmadan mali onay isteyen kararlar" bölümüyle **aynı desen**: uygulanan
> varsayım + karşı görüş + değişecek tek yer.

| # | Soru | Uygulanan varsayım | Karşı görüş | Değişecek tek yer |
|---|---|---|---|---|
| 1 | Ödeme otelde yapılırken de "zahlungspflichtig buchen" zorunlu mu? | **Evet, zorunlu.** §312j ücretli sözleşmeye uygulanır; ödemenin zamanı istisna değildir | Bazı görüşler "ödeme yükümlülüğü sonradan doğuyorsa" daha yumuşak formülasyona izin verildiğini savunur; eşdeğer ifadeler (`kostenpflichtig buchen`) de kabul görür | `legal.orderButton.payable` i18n anahtarı + `mustBeExactLabel` bayrağı |
| 2 | E-posta §312f'in "kalıcı veri taşıyıcısı" şartını karşılar mı? | **Evet**, içerik **gövdede** ise | Yalnızca indirme bağlantısı veren e-posta yeterli sayılmayabilir (içeriğin değiştirilemez biçimde saklanabilmesi aranır) | `IBookingConfirmationSender` şablonu |
| 3 | Sözleşme ne zaman kurulur? | **Anında onay = kabul** (`ConfirmationMode: Instant`) — onay e-postası *Annahme*'dir | Otel "talebi aldık, teyit edeceğiz" modelini (*Zugangsbestätigung*) tercih edebilir; o zaman buton metni ve onay e-postası farklı olmalıdır | `PublicBookingSettings.ConfirmationMode` (`Instant` \| `OnHotelAcceptance`) |
| 4 | Sözleşmenin tarafı otel mi, Head Office mi? | **Otel** (fatura da otel adına kesiliyor; `Hotel.TaxNumber` kullanılıyor) | Zincir/franchise yapılarında satışın marka tüzel kişiliği üzerinden yapıldığı modeller var; o zaman Impressum ve AGB marka düzeyinde olmalı | `HotelLegalProfile` vs. `HeadOffice` düzeyinde künye |
| 5 | Kurtaxe Gesamtpreis'in içinde mi gösterilmeli, ayrı mı? | **İçinde** (`totalGross`'a dâhil) **ve ayrıca ayrı satır** | Bazı belediye uygulamaları ve bazı görüşler *durchlaufender Posten*'in toplam fiyattan ayrı gösterilmesini ister | `price.totalGross` bileşimi (tek yer) |
| 6 | Geç iptal / no-show bedeli KDV'ye tabi mi? | **Karar verilmedi, bilinçli olarak.** Public sözleşme yalnızca yüzde ve tutar bildirir; KDV muamelesi faturalama modülünün açık sorusudur (README) | README'deki iki görüş aynen geçerlidir | `InvoiceAmounts.ResolveVatRate` + satır türü (public tarafta değişiklik yok) |
| 7 | Rezervasyon yapanın ergin olması | Formda **18+ beyanı** alınır (`consents.bookerIsAdult`) | Beyan hukuken sınırlı değer taşır (§§104 ff. BGB); bazı oteller ek önlem ister | `consents` bloğu |
| 8 | DSGVO Art. 17 silme talebi | **Bu fazda self-servis silme ucu YOK.** Faturalanmış konaklama GoBD/AO §147 gereği 10 yıl saklanır; faturalanmamış iptal edilmiş rezervasyon silinebilir | Denetim otoritesi self-servis bir yol bekleyebilir | Ayrı bir faz; şu an manuel süreç |

---

## 11. İş bölümü ve bağımlılık sırası

> **Kural değişmez:** Domain değişikliği → **Database → Backend → Frontend**
> (`docs/agent-responsibilities.md` §3).

```
   [1] Database Agent  ──────────────┐
        şema + kısıtlar + migration  │
                                     ▼
   [2] Backend Agent  ───────────────┐
        tenant ctx, public feature   │
        slice, rate limit, OpenAPI   │
                                     ▼
   [3] Frontend Agent (b) ekranlar   │
                                     ▼
   [4] DevOps Agent  ────────────────┘
        testler + CI + prerender job

   [3a] Frontend Agent — workspace/kütüphane iskeleti  ⇄ [1] ile PARALEL
        (API'ye bağımlı değil, yalnızca bu belgeye)
```

### [1] Database Agent — *ilk, tek başına*
- §7'deki 15 kalemin tamamı: kolonlar, owned type'lar, `BookingHold`, `PublicBooking`,
  `RoomTypeImage`/`HotelImage`, `ReservationChannel.Website`.
- İki `EXCLUDE USING gist` kısıtı (**`Reservations` kısıtı mevcut bir açığı kapatır**, atlanamaz).
- `PublicSlug` ve token hash'leri için partial unique index'ler (`NOT IsDeleted` kuralı).
- Seed: demo otele slug, saat dilimi, iptal politikası, legal profile, `Channel: null` bir rate plan.
- **Bitiş kriteri:** migration `Up`/`Down` çalışır, model doğrulayıcı geçer, tenant filtresi
  `BookingHold` ve `PublicBooking` için otomatik uygulanır (ikisi de `ITenantEntity`).

### [2] Backend Agent — *[1] tamamlandıktan sonra*
- `ITenantContext` + `PublicTenantMiddleware`; `AppDbContext`'in `ICurrentUser`'dan
  `ITenantContext`'e geçişi (admin davranışı **birebir korunur**).
- `Application/Features/Public/**`: 13 uç (sözleşme dosyası), tümü `Public*` DTO'larla.
- `ReservationPricingService`'in oda tipi bazlı aşırı yüklemesi (§8) — **kopyalama yok**.
- Hold yaşam döngüsü + sweeper `HostedService`.
- `IPaymentAuthorizationProvider` + `NullPaymentProvider`; `IBotChallengeVerifier` +
  `NullChallengeVerifier`; `IBookingConfirmationSender` + outbox.
- Rate limiting (uç bazında, sözleşmedeki tablo), 429 + `Retry-After`.
- İkinci OpenAPI belgesi (`public-v1`) ve grup ayrımı.
- Admin tarafı eklemeleri: `PUT /hotels/{id}/settings` içine `publicBooking`, `cancellationPolicy`,
  `legalProfile`; `GET /reservations/{id}` yanıtına `publicReference`;
  `GET /reservations/{id}/public-booking` (rıza anlık görüntüsü, `Reservations.View`).
- **Yeni izin anahtarı YOK** — public uçlar anonim, ayarlar `Settings.Manage` altında.

### [3] Frontend Agent
- **(a) [1] ile paralel, API'ye bağımlı değil — kısmen YAPILMIŞ durumda:** workspace ayrımı,
  `projects/shared` ortak katmanı (`@hotelcore/shared`), tema tokenlarının ve dil sözleşmesinin
  taşınması, admin uygulamasının bunu tüketmeye geçmesi, `guest-web` iskeleti (`home`, `search`,
  `room-type`, `booking`, `confirmation`, `legal` özellik klasörleri, dil önekli route'lar, SEO
  servisi) ve `outputMode: "server"` yapılandırması. **Kalan:** `main.server.ts`/`server.ts`
  girişleri, `prerender-routes.ts` ve §2.2 tablosundaki route→mod atamalarının bağlanması.
- **(b) [2] bittikten sonra:** ara → sonuç → detay → rezervasyon → onay akışı; §312j özet bileşeni;
  hukuki bildirim bileşenleri; Impressum/Datenschutz/AGB sayfaları; onay/iptal ekranları;
  admin tarafında "Public kanal" ayar ekranı.
- Kesin kural: `@hotelcore/shared` içine JWT'ye dokunan hiçbir şey konmaz (§2.1).

### [4] DevOps Agent
- Tenant izolasyonu testi: A otelinin slug'ıyla B otelinin oda tipi/hold/booking token'ı → **404**.
- DTO ayrıklığı testi: public OpenAPI belgesinin şema kümesi admin belgesininkiyle **kesişmez**;
  `Application/Features/Public` içindeki hiçbir tip admin namespace'inden tip referanslamaz.
- Değişmez testi: `Source == PublicChannel ⇒ HotelId != null && !CanAccessAllHotels`.
- **Fiyat eşitliği testi** (§8): public teklif toplamı == üretilen faturanın `grossAmount`'u.
- Eşzamanlılık testi: son oda için N paralel `POST /holds` → tam **1** başarı, N−1 × 409;
  `EXCLUDE` kısıtının 23P01 → 409 çevirisi.
- Yasak alan testi: public yanıt JSON'larında `roomNumber`, `floor`, `housekeepingStatus`,
  `reservationNumber`, `notes` anahtarları **hiç geçmez** (§4.3 listesi).
- Kart tripwire testi: `cardNumber` içeren gövde → 400 `CARD_DATA_NOT_ACCEPTED`, log'da gövde yok.
- Rate limit testi: uç başına eşik aşımında 429 + `Retry-After`.
- Hukuki alan testi: hold yanıtı `orderSummary`, `legal.withdrawalRight`, `legal.orderButton`,
  `price.cityTax` alanlarını **her zaman** taşır (eksikse test kırılır).
- CI: `frontend-ci.yml` iki uygulamayı da derler (`npm run build` sarmalayıcısı) ve **prerender
  çıktısını denetler**: `dist/guest-web/browser/{de,en,tr}/legal/imprint/index.html` içinde künye
  metni yoksa iş akışı kırılır (§2.3). `npm run legal:snapshot:check` derlemeden önce koşar.
  `backend-ci.yml` migration'ların **uygulandığını** ve model ile migration'ların ayrışmadığını
  ayrıca doğrular (`migrations list` → "(Pending)" yok, `has-pending-model-changes`).
  Lighthouse/a11y kontrolü guest app için hâlâ eklenmedi.

---

## 12. Bilinen sınırlar (bu fazda bilinçli olarak yapılmadı)

- **İptal politikası otel bazındadır**, fiyat planı bazında değil. "Non-refundable" tarife
  satılamaz. Plan bazlı politika `RatePlan` üzerine bir owned type ile eklenir; sözleşmedeki
  `cancellation` nesnesi bu genişlemeyi kaldıracak biçimde tasarlanmıştır (`type` alanı vardır).
- **Oda tipi URL anahtarı `code`'dur** (`/zimmer/dbl`), dile göre slug yoktur. Ayrı bir çok dilli
  slug kolonu ikinci bir benzersizlik problemi doğurur; SEO ağırlığı `<title>`/`<h1>` ve dil öneki
  ile taşınır. Gerekirse sonra eklenir.
- **Pansiyon (board) tipi modellenmemiştir**; `essentialFeatures.board` şimdilik sabit `"None"`
  döner. Kahvaltı bir `Extra`'dır ve public tarafta satılmaz.
- **Görseller yalnızca URL olarak** saklanır; yükleme, yeniden boyutlandırma, CDN yoktur.
- **Grup/çoklu oda rezervasyonu yoktur:** bir rezervasyon = bir oda. Çoklu oda, `ReservationGroup`
  ile ayrı bir fazdır.
- **Promosyon kodu / kampanya yoktur** (`promoCode` alanı sözleşmede rezerve edilmemiştir).
- **Self-servis veri silme (Art. 17) ucu yoktur** (§10 madde 8).
- **Erken çıkışta Kurtaxe** hâlâ planlanan geceye göredir (README §14). `Hotel.TimeZoneId` bu fazda
  eklendiği için eksiğin **yarısı** kapanmıştır; fiilî giriş/çıkış **takvim günü** hâlâ tutulmuyor.
