# HotelCore — API Sözleşmeleri

> **Kaynak-of-truth:** Backend'in ürettiği OpenAPI şeması (`/swagger/v1/swagger.json`).
> Bu doküman insan-okunur özettir; çelişki olursa OpenAPI şeması esastır. Tüm endpoint'ler
> `/api/v1` prefix'i altındadır. Tüm hatalar RFC 7807 `ProblemDetails` formatındadır.

## Genel Kurallar
- **Base URL:** `/api/v1`
- **Auth:** `Authorization: Bearer <jwt>` (login hariç tüm endpoint'ler).
- **Aktif otel:** `X-Hotel-Id: <guid>` header'ı (opsiyonel; yoksa JWT'deki varsayılan otel).
  Head Office kullanıcısı bu header'ı boş bırakırsa → konsolide (tüm oteller) görünüm.
- **Dil:** `Accept-Language: de|en|tr` (yoksa kullanıcı profili → yoksa `de`).
- **Sayfalama:** `?page=1&pageSize=20` → yanıt `{ items, page, pageSize, totalCount }`.
- **Hata formatı:** `ProblemDetails` — `{ type, title, status, detail, errors? }`.

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
                  "cityTaxPerPersonNight":3.00, "cityTaxEnabled":true } }

// PUT /hotels/{id}/settings  → 200 + HotelResponse
{ "name":"...", "country":"DE", "city":"Berlin", "addressLine":null, "postalCode":null,
  "phone":null, "email":null, "taxNumber":null, "defaultCulture":"de", "currency":"EUR",
  "taxProfile": { "vatRate":19, "reducedVatRate":7,
                  "cityTaxPerPersonNight":3, "cityTaxEnabled":true } }

// GET/PUT /head-office/settings → HeadOfficeSettingsResponse
{ "id":"guid", "brandName":"HotelCore Group", "defaultCulture":"de", "hotelCount":1 }
```

**Doğrulama:** `name`/`brandName` zorunlu ≤ 200 · `city` zorunlu ≤ 100 · `currency` tam 3 büyük
harf (ISO 4217) · `defaultCulture` ∈ `de|en|tr` · `country` enum adı · `vatRate` ve
`reducedVatRate` 0–100 · `cityTaxPerPersonNight` ≥ 0 · `addressLine` ≤ 200 · `postalCode` ≤ 20 ·
`phone` ≤ 50 · `email` geçerli e-posta ≤ 200 · `taxNumber` ≤ 50.

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
| DELETE | `/rooms/{id}` | `Rooms.Manage` | Soft-delete. Gelecek rezervasyon varsa **409** |
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

### Reservations
| Method | Path | İzin |
|---|---|---|
| GET | `/reservations` | `Reservations.View` |
| POST | `/reservations` | `Reservations.Create` (rezervasyon sihirbazı) |
| POST | `/reservations/{id}/check-in` | `Reservations.CheckInOut` |
| POST | `/reservations/{id}/check-out` | `Reservations.CheckInOut` |
| GET | `/reservations/{id}/folio` | `Reservations.View` (detay çekmecesi) |

### Invoices (GoBD)
| Method | Path | İzin |
|---|---|---|
| GET | `/invoices` | `Invoices.View` |
| POST | `/invoices` | `Invoices.Create` (Draft) |
| POST | `/invoices/{id}/finalize` | `Invoices.Approve` (→ Finalized, değiştirilemez) |
| POST | `/invoices/{id}/cancel` | `Invoices.Cancel` (→ Stornorechnung) |
| GET | `/invoices/{id}/pdf` | `Invoices.View` |

### HR (Staff / Vacation / TimeTracking / Shifts)
| Method | Path | İzin |
|---|---|---|
| GET | `/employees` | `Employees.View` |
| GET/POST | `/vacations` | `Vacations.View` / `Vacations.Request` |
| POST | `/vacations/{id}/approve` | `Vacations.Approve` |
| POST | `/time-entries/clock-in` | `TimeTracking.Record` |
| POST | `/time-entries/clock-out` | `TimeTracking.Record` |
| GET | `/shifts?week=` | `Shifts.View` |

### Reports
| Method | Path | İzin |
|---|---|---|
| GET | `/reports/revenue?from=&to=` | `Reports.View` (ciro, kanal dağılımı, ADR/RevPAR) |
| GET | `/reports/occupancy?from=&to=` | `Reports.View` |

## Frontend Client Üretimi
Backend `dotnet run` ile ayaktayken:
```
cd src/frontend
npx ng-openapi-gen --input http://localhost:5080/swagger/v1/swagger.json --output src/app/core/api
```
> İskelet fazında elle yazılmış tip-güvenli servisler mevcuttur; sözleşme stabilize olunca
> otomatik üretime geçilir. Bu adım `api-integration` skill'inde standartlaştırılmıştır.
