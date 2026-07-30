# HotelCore — API Sözleşmeleri

> **Kaynak-of-truth:** Backend'in ürettiği OpenAPI şeması (`/swagger/v1/swagger.json`).
> Bu doküman insan-okunur özettir; çelişki olursa OpenAPI şeması esastır. Tüm endpoint'ler
> `/api/v1` prefix'i altındadır. Tüm hatalar RFC 7807 `ProblemDetails` formatındadır.

## Genel Kurallar
- **Base URL:** `/api/v1`
- **Auth:** `Authorization: Bearer <jwt>` (login hariç tüm endpoint'ler).

> **İstisna — public kanal:** `/api/v1/public/**` altındaki uçlar **anonimdir**, aktif otel
> `X-Hotel-Id` ile değil **yoldaki `hotelSlug`** ile belirlenir ve şemaları **ayrı bir OpenAPI
> belgesindedir** (`/swagger/public-v1/swagger.json`). Kuralları:
> **[api-contracts-public-booking.md](api-contracts-public-booking.md)**.
- **Aktif otel:** `X-Hotel-Id: <guid>` header'ı (opsiyonel; yoksa JWT'deki varsayılan otel).
  Head Office kullanıcısı bu header'ı boş bırakırsa → konsolide (tüm oteller) görünüm.
- **Dil:** `Accept-Language: de|en|tr` (yoksa kullanıcı profili → yoksa `de`).
- **Sayfalama:** `?page=1&pageSize=20` → yanıt `{ items, page, pageSize, totalCount }`.
- **Hata formatı:** `ProblemDetails` — `{ type, title, status, detail, errors? }`.

**Hata mesajlarının dili.** `title`, `detail` ve `errors` **üçü de** isteğin diline göre döner.
Çözüm sırası: query string → cookie → `Accept-Language` → JWT `culture` claim'i → varsayılan `de`.
Desteklenen diller `de | en | tr`; desteklenmeyen bir dil `de`'ye düşer. Yanıt `Content-Language`
başlığıyla aktif dili bildirir.

> **Mesaj metinleri sözleşmenin parçası DEĞİLDİR** ve dile göre değişir. İstemci mantığı metne
> değil `status` + `type` + `errors` anahtarlarına dayanmalıdır. `errors` anahtarları (PascalCase
> alan adları) ve `type` URI'ları dilden bağımsızdır.

Mesaj gövdesindeki **tarihler her dilde ISO 8601** (`yyyy-MM-dd`), **para tutarları aktif kültüre
göre** biçimlenir (`de: 1.234,50`). Enum/durum adları, alan adları ve teknik terimler
(`Stornorechnung`, `finalize`, `out of order`, `X-Hotel-Id`) çevrilmez.

## JWT Claim Şeması
```
sub          -> userId
email        -> kullanıcı e-postası
headOfficeId -> bağlı head office
perm         -> izin anahtarları (çoklu claim: "Invoices.Approve", "Rooms.View", ...)
hotel        -> erişilebilir hotel id'leri (çoklu claim)
allHotels    -> "true" ise tüm otellere erişim (Head Office bypass)
culture      -> tercih edilen dil
```

## Endpoint Taslağı (v1 — iskelet)

### Auth — **uygulandı** (v1 iskelet tamamlandı)
| Method | Path | İzin | Açıklama |
|---|---|---|---|
| POST | `/auth/login` | anonim | `{ email, password }` → `{ accessToken, refreshToken, expiresAtUtc, tokenType, user }` |
| POST | `/auth/refresh` | anonim | `{ refreshToken }` → yeni token çifti (**`user` YOK**) |
| GET | `/auth/me` | authenticated | `user` nesnesinin birebir aynısı, sarmalayıcısız |
| GET | `/health` | anonim | `{ status, database, durationMs }` — DB bağlantısını da kontrol eder |

**`user` nesnesi** (login yanıtında ve `/auth/me`'de aynı şekil):
```jsonc
{
  "id": "guid", "email": "...", "firstName": "...", "lastName": "...",
  "displayName": null,                 // yoksa first+last, o da yoksa email
  "culture": "de", "headOfficeId": "guid",
  "roles": ["Admin"],
  "permissions": ["Rooms.View", "..."],          // düz string dizisi, Modül.Aksiyon
  "hotels": [{ "id": "guid", "name": "...", "city": "...",
               "country": "DE", "currency": "EUR", "defaultCulture": "de" }],
  "canAccessAllHotels": false,
  "defaultHotelId": "guid"
}
```

**Uygulamadaki davranış notları (frontend bunlara güvenebilir):**
- **Hatalı kimlik bilgisi → 401**, kullanıcı var/yok ayrımı sızdırılmaz (aynı mesaj).
- **Refresh rotating:** kullanılan token iptal edilir (`RevokedAt`, `ReplacedByTokenId`), yenisi verilir. İptal edilmiş bir token yeniden kullanılırsa → o kullanıcının **tüm aktif token'ları iptal edilir** + 401.
- `expiresAtUtc` `Z` sonekli UTC (`2026-07-29T01:19:52.1336092Z`), `+00:00` değil.
- **`X-Hotel-Id`**: bozuk GUID → **400**; kullanıcının erişemediği otel → **403** (endpoint hiç çalışmadan, middleware'de). Head Office kullanıcısı header göndermezse konsolide görünüm.
- Validation hatalarında `errors` anahtarları **PascalCase alan adları** (`"Email"`), gövdenin geri kalanı camelCase. Mesajlar `Accept-Language`'e göre DE/EN/TR.
- JWT'de `allHotels` claim'i **string** (`"true"`/`"false"`); `hotel` claim'i tek otelde string, çok otelde dizi olur. **Token'ı decode etmek yerine `user.hotels` / `user.defaultHotelId` kullanın.**
- Development'ta HTTPS yönlendirmesi kapalı (`http://localhost:5080` doğrudan); Production'da `UseHttpsRedirection` + HSTS aktif.

### Hotels & Ayarlar — **uygulandı**

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/hotels` | `Hotels.View` | Kullanıcının **erişebildiği** oteller (düz dizi) |
| GET | `/hotels/{id}` | `Hotels.View` | Detay + vergi profili |
| PUT | `/hotels/{id}/settings` | `Settings.Manage` | Otel künyesi + `taxProfile` |
| GET | `/head-office/settings` | `Settings.Manage` | Marka adı, varsayılan dil |
| PUT | `/head-office/settings` | `Settings.Manage` | |

> **Erişim kuralı:** `Hotel` tenant-scoped bir entity **değildir** (tenant kökünün kendisidir), bu
> yüzden global query filter onu süzmez. Erişim `UserHotelAccess` tablosundan doğrulanır;
> `allHotels` yetkisi olan kullanıcı kendi Head Office'ine bağlı **tüm** otelleri görür.
> Erişilemeyen bir otel **404** döner (403 değil) — otelin varlığı sızdırılmaz.

```jsonc
// GET /hotels → HotelListItemResponse[]
[{ "id":"guid", "name":"HotelCore Berlin Mitte", "city":"Berlin",
   "country":"DE", "currency":"EUR", "defaultCulture":"de", "roomCount":13 }]

// GET /hotels/{id} → HotelResponse
{ "id":"guid", "headOfficeId":"guid", "name":"...", "country":"DE", "city":"Berlin",
  "addressLine":"...", "postalCode":"10115", "phone":"...", "email":"...",
  "taxNumber":"DE123456789", "defaultCulture":"de", "currency":"EUR", "roomCount":13,
  "taxProfile": { "vatRate":19.00, "reducedVatRate":7.00,
                  "cityTaxPerPersonNight":3.00, "cityTaxEnabled":true,
                  "cityTaxExemptChildren":true,   // Kurtaxe'de cocuklar muaf mi (varsayilan false)
                  "cityTaxChildAgeLimit":18 } }   // null olabilir; HESABA GIRMEZ (asagi bkz.)

// PUT /hotels/{id}/settings  → 200 + HotelResponse
{ "name":"...", "country":"DE", "city":"Berlin", "addressLine":null, "postalCode":null,
  "phone":null, "email":null, "taxNumber":null, "defaultCulture":"de", "currency":"EUR",
  "taxProfile": { "vatRate":19, "reducedVatRate":7,
                  "cityTaxPerPersonNight":3, "cityTaxEnabled":true,
                  "cityTaxExemptChildren":true, "cityTaxChildAgeLimit":18 } }
// taxProfile OKUMA ve YAZMADA ayni sekildedir: GET govdesi dogrudan PUT'a gonderilebilir.
// PUT tam degisimdir — gonderilmeyen alanlar varsayilana (false / null) duser.

// GET/PUT /head-office/settings → HeadOfficeSettingsResponse
{ "id":"guid", "brandName":"HotelCore Group", "defaultCulture":"de", "hotelCount":1 }
```

**Doğrulama:** `name`/`brandName` zorunlu ≤ 200 · `city` zorunlu ≤ 100 · `currency` tam 3 büyük
harf (ISO 4217) · `defaultCulture` ∈ `de|en|tr` · `country` enum adı · `vatRate` ve
`reducedVatRate` 0–100 · `cityTaxPerPersonNight` ≥ 0 · `addressLine` ≤ 200 · `postalCode` ≤ 20 ·
`phone` ≤ 50 · `email` geçerli e-posta ≤ 200 · `taxNumber` ≤ 50.

**Kurtaxe çocuk muafiyeti:**
- `cityTaxExemptChildren` ≤ tek hesaplanabilir bilgi: `true` ise vergiye tabi kişi sayısı
  **yalnızca `adults`**'tır (`adults + children` değil) → çocuklu rezervasyonlarda Kurtaxe **düşer**.
  Varsayılan `false`, yani muafiyet **opt-in**; mevcut oteller etkilenmez.
- `cityTaxChildAgeLimit` (null veya **0–99**) **hesaba girmez**. Rezervasyon yalnızca yetişkin/çocuk
  **sayısı** tutar, doğum tarihi tutmaz; bu yüzden yaşa göre ayrıştırma yapılamaz. Değer faturada
  muafiyetin hukuki dayanağı olarak basılır ve "çocuk" tanımını belgeler.
- Muafiyet açıkken yaş sınırı **zorunlu değildir** (otel bilmiyor olabilir). Sınır, muafiyet
  kapalıyken de saklanır — belediye kuralı geçici kapatmada kaybolmasın.
- Aralık dışı değer → **400**, hata anahtarı `TaxProfile.CityTaxChildAgeLimit`.

> **Vergi oranları koda hardcode edilmez** (architecture.md §4.1) — burada yönetilir ve
> faturalama bu değerleri okur.

### Rooms & Housekeeping — **uygulandı**

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/room-types` | `Rooms.View` | Düz dizi (oda tipi sayısı az, sayfalama yok) |
| GET | `/room-types/{id}` | `Rooms.View` | |
| POST | `/room-types` | `Rooms.Manage` | 201 + `Location` |
| PUT | `/room-types/{id}` | `Rooms.Manage` | |
| DELETE | `/room-types/{id}` | `Rooms.Manage` | Soft-delete. Bağlı oda varsa **409** |
| GET | `/rooms` | `Rooms.View` | Sayfalı + filtreli |
| GET | `/rooms/{id}` | `Rooms.View` | |
| POST | `/rooms` | `Rooms.Manage` | 201 + `Location` |
| PUT | `/rooms/{id}` | `Rooms.Manage` | |
| DELETE | `/rooms/{id}` | `Rooms.Manage` | Soft-delete. Gelecek rezervasyon **veya faturalanmamış rezervasyon** varsa **409** (GoBD / AO §147) |
| GET | `/rooms/board` | `Housekeeping.View` | Kat bazlı pano — **finansal alan İÇERMEZ** |
| PATCH | `/rooms/{id}/housekeeping` | `Housekeeping.Update` | |

> **RBAC kritik kuralı** (architecture.md §7): Housekeeping rolü fiyat/ciro görmez. Bu yüzden
> `basePrice` **yalnızca** `Rooms.View` gerektiren uçlarda döner; `/rooms/board` yanıtında
> hiçbir para alanı yoktur. Frontend'de gizlemek yeterli değildir — kural backend'de uygulanır.

#### Çeviri davranışı (dinamik içerik)
`RoomType.name` / `description` çok dillidir (`Translation` tablosu, §4.6). Yanıtlarda
**`Accept-Language`'e göre çözümlenmiş** metin döner; o dilde çeviri yoksa entity'deki
varsayılan değere düşer. Yazma uçlarında çeviriler opsiyoneldir:
```jsonc
"translations": { "de": { "name": "Doppelzimmer", "description": "..." },
                  "en": { "name": "Double Room" },
                  "tr": { "name": "İki Kişilik Oda" } }
```
`GET /room-types/{id}` yanıtında düzenleme ekranı için **tüm** çeviriler `translations`
alanında birlikte döner; liste yanıtında dönmez.

#### Şekiller
```jsonc
// RoomTypeResponse
{ "id":"guid", "code":"DBL", "name":"Doppelzimmer", "description":"...",
  "basePrice":120.00, "currency":"EUR", "capacity":2, "sizeSqm":24,
  "amenities":["wifi","minibar","balcony"],     // DB'de virgüllü string, API'de DİZİ
  "roomCount":12 }

// RoomResponse
{ "id":"guid", "number":"201", "floor":2,
  "roomTypeId":"guid", "roomTypeCode":"DBL", "roomTypeName":"Doppelzimmer",
  "housekeepingStatus":"Dirty",                  // enum ADI (string), sayı değil
  "isOutOfOrder":false, "note":null }

// GET /rooms → PagedResult<RoomResponse>
// Filtreler: ?page=1&pageSize=20&roomTypeId=&floor=&housekeepingStatus=&search=
//   search → oda numarasında contains (case-insensitive)
//   siralama: floor, sonra number (numerik-dogal siralama)

// GET /rooms/board → finansal alan YOK
{ "floors": [ { "floor": 2,
                "rooms": [ { "id":"guid", "number":"201", "roomTypeCode":"DBL",
                             "housekeepingStatus":"Dirty", "isOutOfOrder":false,
                             "note":null } ] } ],
  "summary": { "clean":18, "dirty":5, "inspected":2, "outOfOrder":1, "total":26 } }

// PATCH /rooms/{id}/housekeeping
{ "status": "Inspected", "note": "Minibar dolduruldu" }   // note opsiyonel; null gonderilirse temizlenir
// → 200 + RoomResponse
```

#### Yazma gövdeleri
```jsonc
// POST/PUT /room-types
{ "code":"SUI", "name":"Suite", "description":"Grosse Suite",   // name = varsayilan dil metni (zorunlu)
  "basePrice":320.50, "capacity":4, "sizeSqm":58,               // sizeSqm TAM SAYI (int?)
  "amenities":["wifi","minibar"],                               // opsiyonel
  "translations":{ "de":{...}, "en":{...}, "tr":{...} } }        // opsiyonel; bir alan null -> o ceviri silinir

// POST/PUT /rooms
{ "number":"201", "floor":2, "roomTypeId":"guid",
  "housekeepingStatus":"Clean",   // opsiyonel, varsayilan Clean
  "isOutOfOrder":false,           // opsiyonel; durumla tutarliligi sunucu korur
  "note":null }                   // opsiyonel
```

> **Yazma işlemleri aktif otel gerektirir.** Head Office kullanıcısı `X-Hotel-Id` göndermezse
> konsolide moddadır ve kaydın hangi otele yazılacağı belirsizdir → **400**
> (`errors: { "X-Hotel-Id": ["Kayıt oluşturmak için aktif otel seçilmelidir..."] }`).
> Okuma uçlarında konsolide mod geçerlidir.

#### Doğrulama kuralları (400 + `errors`)
- `code`: zorunlu, 1–10 karakter, otel içinde **unique** → çakışma **409**
- `number`: zorunlu, 1–10 karakter, otel içinde **unique** → çakışma **409**
- `basePrice` ≥ 0, `capacity` 1–20, `sizeSqm` > 0 veya null (tam sayı), `floor` −5…99
- Uzunluk sınırları: `name` ≤ 150, `description` ≤ 1000, `note` ≤ 500
- **Unique çakışmaları soft-delete edilmiş kayıtları saymaz** — silinen bir oda numarası /
  oda tipi kodu yeniden kullanılabilir (unique index'ler `NOT IsDeleted` ile filtrelidir)
- `housekeepingStatus`: `Clean | Dirty | Inspected | OutOfOrder`
- `isOutOfOrder = true` ↔ `housekeepingStatus = OutOfOrder` **tutarlı tutulur**: durum
  `OutOfOrder`'a çekilirse `isOutOfOrder` true olur; `OutOfOrder`'dan çıkılırsa false olur

| GET | `/occupancy?from=&to=` | `Reservations.View` (doluluk grid'i — rezervasyon modülüyle gelecek) |

### Rezervasyon (Guest / RatePlan / Availability / Reservation) — **uygulandı**

Bu modülün sözleşmesi kendi dosyasındadır: **[api-contracts-reservations.md](api-contracts-reservations.md)**
(misafir, fiyat planı, müsaitlik, doluluk planı, rezervasyon yaşam döngüsü, folio).

Bilinmesi gereken kararlar:
- Çakışma **yarı açık aralık** `[checkIn, checkOut)` üzerinden hesaplanır — bir odanın çıkış günü
  aynı gün başka bir rezervasyonun giriş günü **olabilir**.
- `totalAmount` **sunucuda** hesaplanır ve her gece ayrı fiyatlanır; istemciden gelen tutar yok
  sayılır. Gece başına öncelik: kanala özel plan → tüm kanallar planı → `RoomType.BasePrice`.
- Check-out oda durumunu **otomatik `Dirty`** yapar (architecture.md §5), aynı transaction'da.
- `ReservationNumber` (`RES-2026-00001`) **fatura numarası değildir**; GoBD kesintisizliği yalnızca
  faturaya özgüdür, burada boşluk kabul edilir.
- Doluluk planı en fazla **92 gün** ister (aşınca 400).

### Faturalama (GoBD) — **uygulandı**

Bu modülün sözleşmesi kendi dosyasındadır: **[api-contracts-invoices.md](api-contracts-invoices.md)**
(taslak, kesinleştirme, Stornorechnung, ödeme, denetim izi).

Bilinmesi gereken kararlar:
- Fatura numarası **yalnızca kesinleştirme anında** atanır ve otel + yıl bazında kesintisizdir;
  yarışı kaybeden istek **numara tüketmez** (409 alır).
- Kesinleşmiş fatura **değiştirilemez** (PUT → 409); düzeltme Stornorechnung ile yapılır, orijinal
  aynen korunur.
- KDV oranları `Hotel.TaxProfile`'dan okunur: konaklama indirimli oran, ekstralar standart oran,
  **Kurtaxe KDV dışıdır** (belediye vergisi, otel yalnızca tahsil eder).
- Birim fiyatlar **brüt** kabul edilir; yuvarlama satır bazında 2 ondalık, `net + vat == gross`
  her zaman korunur.
- `GET /invoices/{id}/pdf` → **501**: belge üretimi bu fazda yok, sahte PDF döndürülmez.

### Personel (Employees & Departments) — **uygulandı**

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/departments` | `Employees.View` | Düz dizi (departman sayısı az) |
| POST | `/departments` | `Employees.Edit` | Ad otel içinde unique → **409** |
| PUT | `/departments/{id}` | `Employees.Edit` | |
| DELETE | `/departments/{id}` | `Employees.Edit` | **Hard delete**; bağlı çalışan varsa **409** |
| GET | `/employees` | `Employees.View` | Sayfalı + filtreli |
| GET | `/employees/{id}` | `Employees.View` | |
| POST | `/employees` | `Employees.Edit` | `staffNumber` unique → **409** |
| PUT | `/employees/{id}` | `Employees.Edit` | |
| DELETE | `/employees/{id}` | `Employees.Edit` | Soft-delete |

> `Department` **soft-delete edilemez** (kasıtlı): departman bir sınıflandırmadır, geçmiş kayıt
> taşımaz. Bu yüzden silme gerçek silmedir ve bağlı çalışan varken engellenir.

```jsonc
// DepartmentResponse
{ "id":"guid", "name":"Rezeption", "description":"...", "employeeCount":4 }

// EmployeeResponse
{ "id":"guid", "firstName":"Anna", "lastName":"Becker", "fullName":"Anna Becker",
  "email":"anna@hotel.de", "phone":null, "staffNumber":"P-014",
  "departmentId":"guid", "departmentName":"Rezeption",
  "employmentType":"FullTime",            // enum ADI (string)
  "annualLeaveDays":28.00,
  "hiredOn":"2024-03-01", "terminatedOn":null,
  "isActive":true,                        // terminatedOn yok veya gelecekte
  "userId":"guid|null" }                  // login iliskisi (opsiyonel)

// GET /employees → PagedResult<EmployeeResponse>
// Filtreler: ?page=1&pageSize=20&departmentId=&employmentType=&search=&includeTerminated=false
//   search  -> ad, soyad ve personel numarasinda contains (case-insensitive)
//   siralama: lastName, firstName
//   includeTerminated=false (varsayilan) -> isten ayrilmislar listelenmez

// POST/PUT /employees
{ "firstName":"Anna", "lastName":"Becker", "email":null, "phone":null,
  "staffNumber":"P-014", "departmentId":"guid", "employmentType":"FullTime",
  "annualLeaveDays":28, "hiredOn":"2024-03-01", "terminatedOn":null }
```

**Doğrulama:** `firstName`/`lastName` zorunlu ≤ 100 · `email` geçerli ≤ 200 · `phone` ≤ 50 ·
`staffNumber` ≤ 20 (otel içinde unique) · `departmentId` **aynı otelde** olmalı, değilse **404** ·
`employmentType` ∈ `FullTime | PartTime | MiniJob | Apprentice | Seasonal | Temporary` ·
`annualLeaveDays` 0–60 · `hiredOn` zorunlu · `terminatedOn` ≥ `hiredOn` ·
departman `name` zorunlu ≤ 100, `description` ≤ 500.

### İzin (Urlaub) — **uygulandı**

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/vacations` | `Vacations.View` | Sayfalı + filtreli |
| GET | `/vacations/{id}` | `Vacations.View` | |
| POST | `/vacations` | `Vacations.Request` | Talep `Pending` olarak açılır |
| POST | `/vacations/{id}/approve` | `Vacations.Approve` | Bakiyeden **düşer** |
| POST | `/vacations/{id}/reject` | `Vacations.Approve` | Bakiyeyi **etkilemez** |
| POST | `/vacations/{id}/cancel` | `Vacations.Approve` **veya** `Vacations.Request` | Onaylıysa bakiyeyi **geri verir** |
| GET | `/vacations/balances?employeeId=&year=` | `Vacations.View` | |

```jsonc
// VacationRequestResponse
{ "id":"guid", "employeeId":"guid", "employeeName":"Anna Becker",
  "from":"2026-08-10", "to":"2026-08-14",
  "requestedDays":5.00,                  // takvim günü (bkz. not)
  "status":"Pending",                    // Pending | Approved | Rejected | Cancelled
  "reason":null, "decidedByUserId":null, "decidedAt":null, "decisionNote":null,
  "createdAt":"2026-07-30T12:00:00+00:00" }   // talep tarihi

// GET /vacations/balances → VacationBalanceResponse[]  (DÜZ DİZİ, tek nesne degil)
// employeeId verilmezse tum kadro doner; year verilmezse sunucunun gecerli yili kullanilir.
// Kayit yoksa satir calisanin annualLeaveDays'inden turetilir ve `id` null gelir.
[{ "id":null,
   "employeeId":"guid", "employeeName":"Anna Becker", "year":2026,
   "entitledDays":28.00, "usedDays":0, "carriedOverDays":0, "remainingDays":28.00 }]

// POST /vacations           { "employeeId":"guid", "from":"2026-08-10", "to":"2026-08-14", "reason":null }
// POST /vacations/{id}/reject { "decisionNote":"..." }   // gövde opsiyonel
// GET  /vacations filtreleri: ?page&pageSize&employeeId=&status=&year=&from=&to=
```

> **`requestedDays` takvim günüdür** — hafta sonu ve resmî tatil mantığı bu fazda **yoktur**.
> Bordroya bağlanacaksa iş günü hesabı ayrı bir karar olarak eklenmelidir.

**İş kuralları:** `to >= from` · aynı çalışan için tarih aralığı çakışan `Pending`/`Approved`
talep varsa **409** · yalnızca `Pending` onaylanıp reddedilebilir, karara bağlanmış talebe tekrar
karar **409** · onay ve bakiye güncellemesi **tek transaction** · tek talep en fazla **366 gün** ·
`reason` ve `decisionNote` ≤ 500 karakter.

> **İptal yetkisi iki alternatiflidir** ve endpoint'te tek bir policy ile ifade edilemez:
> `Vacations.Approve` olan **her** talebi iptal edebilir; yalnızca `Vacations.Request` olan
> **kendi** talebini iptal edebilir (çalışanın `userId`'si ile eşleşme). Başkasının talebini
> iptal denemesi **403** döner. İstemci bu yüzden aksiyonu "Request ∪ Approve" olarak gösterir.

### Zeiterfassung (TimeEntry) — **uygulandı**

| Method | Path | İzin |
|---|---|---|
| GET | `/time-entries` | `TimeTracking.View` |
| POST | `/time-entries/clock-in` | `TimeTracking.Record` |
| POST | `/time-entries/clock-out` | `TimeTracking.Record` |
| PUT | `/time-entries/{id}` | `TimeTracking.Record` |
| DELETE | `/time-entries/{id}` | `TimeTracking.Record` |

```jsonc
// TimeEntryResponse
{ "id":"guid", "employeeId":"guid", "employeeName":"Anna Becker",
  "clockIn":"2026-07-29T06:00:00+00:00", "clockOut":"2026-07-29T14:30:00+00:00",
  "breakMinutes":30,
  "workedMinutes":480,                   // (clockOut - clockIn) - mola; acik kayitta null
  "source":"Manual", "note":null, "isOpen":false }

// POST /time-entries/clock-in  { "employeeId":"guid", "clockIn":null, "note":null }
// POST /time-entries/clock-out { "employeeId":"guid", "clockOut":null, "breakMinutes":30, "note":null }
//   clockIn/clockOut opsiyoneldir: verilmezse sunucu saati kullanilir (geriye donuk kayit icin acik).
//   Tek kayit ucu (GET /time-entries/{id}) YOKTUR — duzeltme ekrani listeden beslenir.
// PUT  /time-entries/{id}      { "clockIn":"...", "clockOut":"...", "breakMinutes":30, "note":"..." }
// GET  filtreleri: ?page&pageSize&employeeId=&from=&to=
```

**İş kuralları:** açık kayıt varken ikinci clock-in **409** · açık kayıt yokken clock-out **409** ·
`clockOut > clockIn` · `breakMinutes` 0–1440 **ve çalışma süresini aşamaz** (aşarsa 400, mesaj
mevcut süreyi söyler) · gelecek tarihli clock-in reddedilir.

### Vardiya (Shift) — **uygulandı**

| Method | Path | İzin |
|---|---|---|
| GET | `/shifts?week=YYYY-Www` veya `?from=&to=` | `Shifts.View` |
| POST | `/shifts` | `Shifts.Edit` |
| PUT | `/shifts/{id}` | `Shifts.Edit` |
| DELETE | `/shifts/{id}` | `Shifts.Edit` |

```jsonc
// GET /shifts?week=2026-W32 → gun bazli plan
{ "from":"2026-08-03", "to":"2026-08-09",
  "week":"2026-W32",                      // ?from=&to= ile sorulursa null doner
  // Izgaranin SATIR kaynagi: ayrica GET /employees cagirmak gerekmez.
  "employees":[ { "id":"guid", "fullName":"Anna Becker", "departmentName":"Rezeption" } ],
  "days":[ { "date":"2026-08-03",
             "shifts":[ { "id":"guid", "employeeId":"guid", "employeeName":"Anna Becker",
                          "date":"2026-08-03", "shiftType":"Morning", "note":null } ] } ] }

// POST/PUT /shifts { "employeeId":"guid", "date":"2026-08-03", "shiftType":"Morning", "note":null }
// shiftType: Morning | Evening | Night | Off
```

**İş kuralları:** `(employeeId, date)` benzersiz — aynı güne ikinci vardiya **409** · çalışan aktif
otelde olmalı, değilse **404** · ISO hafta (`YYYY-Www`) Pazartesi–Pazar aralığına çevrilir.

### Raporlama — **uygulandı**

Bu modülün sözleşmesi kendi dosyasındadır: **[api-contracts-reports.md](api-contracts-reports.md)**
(`GET /reports/occupancy`, `GET /reports/revenue`, her ikisi `Reports.View`).

Bilinmesi gereken tanımlar — **bu modülde asıl zorluk kod değil, tanımların tutarlılığıdır**:
- **Satılan oda-gece** rezervasyon modülünün çakışma kuralından (`AvailabilityQuery`) türetilir, bu
  yüzden doluluk raporu ile oda takvimi **asla çelişmez**. `Cancelled`/`NoShow` satılmış sayılmaz.
- **Servis dışı odalar müsait kapasiteden düşülür** (tadilattaki oda satılabilir envanter değildir),
  ama `physicalRoomNights`, `outOfOrderRoomNights` ve `availableRoomNights` **üçü de ayrı ayrı**
  döner — tüketici kendi tanımını kurabilir.
- **ADR** = oda geliri / satılan oda-gece; ekstralar ve Kurtaxe **girmez**.
  **RevPAR** = oda geliri / müsait oda-gece. Net ve brüt sürümleri **ayrı alanlardır**.
- **Ciro kesinleşmiş faturalardan** okunur (`issuedAt != null`), yani muhasebeyle uzlaşır.
  Taslaklar sayılmaz. Kesinleşip iptal edilen fatura **ve** onun Stornorechnung'u **birlikte**
  sayılır; ikisi tam sıfır eder — yalnızca storno sayılsaydı rapor hayali negatif ciro gösterirdi.
- **Kurtaxe gelir değildir** (`cityTaxCollected` ayrı alan, `totalRevenue`'ya ve ADR'ye girmez).
- Faturalanmamış konaklamalar `unbilledRoomRevenueGross` alanında ayrı durur, hiçbir toplama girmez.
- Konsolide modda rapor çalışır; `scope` alanı hangi kapsamda hesaplandığını söyler ve `byHotel`
  kırılımı **her zaman** döner (karışık para biriminde `currency: null` + `hasMixedCurrencies`).
- Aralık üst sınırı **366 gün** (aşınca 400).

> **Bilinen sınır:** `Room.IsOutOfOrder` tarih aralığı taşımayan **anlık** bir bayraktır; geçmiş
> dönem raporları bugünkü servis dışı odalara göre hesaplanır. Tarihsel doğruluk için tarih aralıklı
> bir `RoomBlock` kaydı gerekir (şema değişikliği, bu fazda yapılmadı). Aynı sebeple doluluk oranı
> **%100'ü aşabilir** ve bilinçli olarak kırpılmaz.

### Misafire Açık (Public) Rezervasyon Kanalı — **planlı**

Bu modülün sözleşmesi kendi dosyasındadır:
**[api-contracts-public-booking.md](api-contracts-public-booking.md)** (otel/marka künyesi,
Impressum, oda tipi kataloğu, müsaitlik + fiyat teklifi, hold, rezervasyon, sorgulama, iptal).
Mimari kararlar: **[architecture-public-booking.md](architecture-public-booking.md)**.

Bilinmesi gereken kararlar:
- **Base URL `/api/v1/public`, tüm uçlar anonim.** Otel yoldaki `hotelSlug` ile belirlenir;
  `Authorization` ve `X-Hotel-Id` header'ları **yok sayılır**. Public uçlar **401/403 üretmez** —
  her yetki/varlık sorunu **404**'e indirgenir.
- **Public DTO'lar admin DTO'larından ayrıdır** (`Public*` öneki, ayrı OpenAPI belgesi). Oda
  numarası, kat, housekeeping durumu, `reservationNumber`, iç notlar, doluluk/ciro alanları
  public yanıtlarda **hiç bulunmaz**; kimlikler GUID değil `hotelSlug`/`roomTypeCode`/
  `bookingReference`'tır.
- **Fiyat ve müsaitlik tek kaynaktan:** `ReservationPricingService`, `InvoiceAmounts`,
  `TaxProfile.CountTaxablePersons`, `CityTaxLiability` ve `AvailabilityQuery` **yeniden kullanılır**;
  public teklifin toplamı üretilen faturanın `grossAmount`'una **kuruşu kuruşuna eşittir**.
- **15 dakikalık hold** (`BookingHold`) vardır: fiyat/özet donar, oda sunucuda pinlenir.
  Yarışın son güvencesi `Reservations` üzerindeki yeni **`EXCLUDE USING gist`** kısıtıdır
  (`daterange(CheckIn, CheckOut, '[)')`) — bu kısıt bugünkü admin tarafı çift rezervasyon
  açığını da kapatır.
- **Kart verisi hiçbir uçta kabul edilmez** (PCI-DSS kapsam dışılığı); gövdede kart alanı adı
  geçerse **400 `CARD_DATA_NOT_ACCEPTED`**. Ödeme "girişte" (`PayAtProperty`), gerçek PSP
  `IPaymentAuthorizationProvider` soyutlamasının arkasındadır.
- Her public hata yanıtı `extensions.code` içinde **dilden bağımsız** stabil bir anahtar taşır
  (`HOLD_EXPIRED`, `SUMMARY_CHANGED`, …); admin uçları bu alanı bu fazda taşımaz.
- Yeni kanal değeri **`ReservationChannel.Website`** eklenir; `Channel = Direct` fiyat planları
  web rezervasyonlarına **uygulanmaz**.

## Frontend Client Üretimi
Backend `dotnet run` ile ayaktayken:
```
cd src/frontend
npx ng-openapi-gen --input http://localhost:5080/swagger/v1/swagger.json --output src/app/core/api
```
> İskelet fazında elle yazılmış tip-güvenli servisler mevcuttur; sözleşme stabilize olunca
> otomatik üretime geçilir. Bu adım `api-integration` skill'inde standartlaştırılmıştır.
