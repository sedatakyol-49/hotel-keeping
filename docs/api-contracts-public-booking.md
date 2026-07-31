# HotelCore — API Sözleşmesi: Misafire Açık (Public) Rezervasyon Kanalı

> **Kaynak-of-truth:** backend'in ürettiği **public** OpenAPI şeması
> (`/swagger/public-v1/swagger.json` — admin şemasından **ayrı belge**). Bu doküman insan-okunur
> özettir; çelişki olursa OpenAPI esastır.
> Mimari kararlar ve gerekçeleri: **[architecture-public-booking.md](architecture-public-booking.md)**.
> Genel kurallar (ProblemDetails biçimi, dil çözümleme, tarih/para biçimi) ana dosyadaki
> [api-contracts.md](api-contracts.md) "Genel Kurallar" bölümünden **aynen** devam eder.

Kapsanan alanlar: **marka/otel künyesi**, **hukuki bilgiler**, **oda tipi kataloğu**,
**müsaitlik + fiyat teklifi**, **geçici tutma (hold)**, **rezervasyon oluşturma**,
**rezervasyon sorgulama**, **iptal**.

---

## 1. Genel kurallar (public'e özgü)

- **Base URL:** `/api/v1/public`
- **Auth:** **yok.** Tüm uçlar `[AllowAnonymous]`. `Authorization` header'ı gönderilse bile
  **tamamen yok sayılır** (admin token public uçta hiçbir ek yetki vermez).
- **Aktif otel:** **yol parametresi `hotelSlug`**. `X-Hotel-Id` header'ı public uçlarda
  **yok sayılır** (400 üretmez; otorite yoldadır).
- **Dil:** `Accept-Language: de|en|tr`; yoksa otelin `defaultCulture`'ı. Yanıt `Content-Language`
  taşır. Çok dilli içerik (oda tipi adı/açıklaması, hukuki metinler) `Translation` tablosundan
  çözülür, çeviri yoksa otelin varsayılan dilindeki metne düşer.
- **Hata formatı:** RFC 7807 `ProblemDetails` — admin tarafıyla **birebir aynı** biçim
  (`type`, `title`, `status`, `detail`, `errors?`, `traceId`).
- **`code` uzantısı (public'e özgü ek):** her public hata yanıtı `extensions.code` içinde
  **dilden bağımsız, stabil** bir anahtar taşır (`HOLD_EXPIRED`, `SUMMARY_CHANGED`, …). İstemci
  mantığı `status` + `code`'a dayanır, **mesaj metnine asla**. Admin uçları bu alanı bu fazda
  taşımaz; yokluğu bir hata değildir ve istemci `code` yoksa yalnızca `status`'e düşmelidir.
- **Çerez:** public uçlar **hiçbir çerez koymaz** (§25 TDDDG — bkz. mimari §9.5).
- **Cache:** katalog/künye uçları `Cache-Control: public, max-age=300`; müsaitlik, hold, booking
  uçları **`Cache-Control: no-store`**.
- **Kimlik biçimi:** public tarafta GUID **kullanılmaz**. Otel → `hotelSlug`, oda tipi →
  `roomTypeCode`, rezervasyon → `bookingReference`, hold → `holdToken`.
- **Tarihler:** `yyyy-MM-dd` (konaklama günleri), mutlak anlar ISO 8601 **otel yerel offset'iyle**
  (`2026-08-07T18:00:00+02:00`) — misafirin takvimiyle karşılaştırabilmesi için UTC değil.
- **Para:** JSON'da her zaman nokta ondalıklı `decimal` (`468.00`); biçimleme istemcinin işidir.

### 1.1 Uç listesi

| # | Method | Path | Hız sınırı | Not |
|---|---|---|---|---|
| 1 | GET | `/public/brands/{brandSlug}/hotels` | 120/dk | Marka sitesi otel listesi |
| 2 | GET | `/public/hotels/{hotelSlug}` | 120/dk | Otel künyesi + politikalar |
| 3 | GET | `/public/hotels/{hotelSlug}/legal` | 120/dk | Impressum, Datenschutz, AGB |
| 4 | GET | `/public/hotels/{hotelSlug}/room-types` | 120/dk | Katalog |
| 5 | GET | `/public/hotels/{hotelSlug}/room-types/{roomTypeCode}` | 120/dk | Detay |
| 6 | GET | `/public/hotels/{hotelSlug}/availability` | 60/dk | Arama + fiyat teklifi (hold **yok**) |
| 7 | POST | `/public/hotels/{hotelSlug}/holds` | 10/dk, 40/saat | Teklifi dondurur (15 dk) |
| 8 | GET | `/public/hotels/{hotelSlug}/holds/{holdToken}` | 60/dk | Kalan süre + donmuş teklif |
| 9 | DELETE | `/public/hotels/{hotelSlug}/holds/{holdToken}` | 30/dk | Serbest bırak |
| 10 | POST | `/public/hotels/{hotelSlug}/bookings` | 5/saat (IP), 3/saat (e-posta) | Rezervasyon oluştur |
| 11 | GET | `/public/hotels/{hotelSlug}/bookings/{accessToken}` | 30/dk | Sorgulama |
| 12 | POST | `/public/hotels/{hotelSlug}/bookings/{accessToken}/cancel` | 10/saat | İptal |
| 13 | POST | `/public/hotels/{hotelSlug}/bookings/lookup` | 5/saat | Bağlantıyı e-postayla yeniden gönder |

> `lookup` yol çakışması yaratmaz: `accessToken` **27 karakter base64url**'dür, `lookup` asla
> geçerli bir token deseni değildir; ayrıca route şablonları farklıdır.

### 1.2 Hız sınırı sözleşmesi

- **Bölümleme anahtarı:** `(hotelSlug, istemci IP)`. IPv4 `/32`, IPv6 **`/64`** (aynı aboneye
  verilen prefix tek istemci sayılır). Ters proxy arkasında `X-Forwarded-For` **yalnızca güvenilen
  proxy listesinden** okunur.
- Aşımda **429** + `Retry-After` (saniye) + `ProblemDetails` (`code: RATE_LIMIT_EXCEEDED`).
  `detail` **hangi eşiğin aşıldığını söylemez** (bilgi sızıntısı yapmamak için).
- `POST /bookings` ayrıca **e-posta bazında** sınırlanır: anahtar `(hotelSlug, SHA-256(lower(email)))`.
  Ham e-posta hız sınırı deposunda **saklanmaz**.
- Sınırlar `appsettings` üzerinden yapılandırılır; **koda gömülmez**. Tablodaki değerler
  varsayılandır.

---

## 2. Otel ve marka künyesi

### 2.1 `GET /api/v1/public/brands/{brandSlug}/hotels`

Bir markanın **public kanalı açık** otelleri. Marka sitesinin otel seçiciyi ve prerender listesini
buradan besler.

```jsonc
// 200 → PublicHotelListItemResponse[]   (DÜZ DİZİ, sayfalama yok — otel sayısı azdır)
[{
  "slug": "berlin-mitte",
  "name": "HotelCore Berlin Mitte",       // Hotel.Name — MARKA/OTEL ADI HARDCODE DEĞİL
  "city": "Berlin",
  "country": "DE",
  "currency": "EUR",
  "defaultCulture": "de",
  "shortDescription": "…",                 // ceviri tablosundan cozulur
  "image": { "url": "https://…/hero.jpg", "alt": "…", "width": 1600, "height": 900 }
}]
```

**404 `BRAND_NOT_FOUND`:** slug yok **veya** markanın public kanalı açık hiçbir oteli yok
(varlık sızdırılmaz).

### 2.2 `GET /api/v1/public/hotels/{hotelSlug}`

```jsonc
// 200 → PublicHotelResponse
{
  "slug": "berlin-mitte",
  "brandName": "HotelCore Group",          // HeadOffice.BrandName
  "name": "HotelCore Berlin Mitte",
  "description": "…",
  "addressLine": "Chausseestrasse 1", "postalCode": "10115",
  "city": "Berlin", "country": "DE",
  "phone": "+49 30 5550000", "email": "info@…",
  "currency": "EUR",
  "timeZoneId": "Europe/Berlin",
  "defaultCulture": "de",
  "supportedCultures": ["de", "en", "tr"],
  "checkInFromLocal": "15:00",
  "checkOutUntilLocal": "11:00",
  "images": [ { "url": "…", "alt": "…", "width": 1600, "height": 900, "sortOrder": 0 } ],
  "amenities": ["wifi", "parking", "breakfast"],

  "booking": {
    "minNights": 1,
    "maxNights": 30,
    "maxAdvanceDays": 365,
    "minAdvanceHours": 0,                  // 0 = ayni gun rezervasyon serbest
    "maxAdults": 10, "maxChildren": 10,
    "confirmationMode": "Instant"          // Instant | OnHotelAcceptance  (bkz. mimari §10.3)
  },

  "cityTax": {                             // Hotel.TaxProfile'dan — KODA GOMULU DEGIL
    "applies": true,
    "perPersonNight": 3.00,
    "currency": "EUR",
    "childrenExempt": true,
    "childAgeLimit": 18,                   // null olabilir; HESABA GIRMEZ, bilgilendirmedir
    "chargedOnlyIfStayTakesPlace": true
  },

  "cancellationPolicy": {
    "type": "Flexible",
    "freeCancellationDaysBeforeArrival": 3,
    "cutoffLocalTime": "18:00",
    "lateCancellationFeePercent": 90.00,
    "noShowFeePercent": 90.00,
    "appliesToAccommodationOnly": true      // Kurtaxe ceza matrahina GIRMEZ
  },

  "paymentOptions": [
    { "method": "PayAtProperty", "requiresGuarantee": false, "description": null }
  ]
}
```

**404 `HOTEL_NOT_FOUND`:** slug yok, otel soft-delete, **veya** `publicBooking.isEnabled == false`.
Üç durum **ayırt edilmez** — 403 dönmek otelin varlığını doğrulardı (admin tarafındaki
"erişilemeyen otel 404" kararıyla aynı çizgi).

### 2.3 `GET /api/v1/public/hotels/{hotelSlug}/legal`

§5 DDG (Impressum), DSGVO Art. 13 (aydınlatma) ve AGB. **Prerender edilen sayfaların kaynağıdır.**

```jsonc
// 200 → PublicLegalResponse
{
  "imprint": {                              // §5 DDG — TAMAMI DB'DEN, hardcode YOK
    "legalEntityName": "HotelCore Berlin Betriebs GmbH",
    "legalForm": "GmbH",
    "representedBy": "Anna Becker (Geschaeftsfuehrerin)",
    "addressLine": "Chausseestrasse 1", "postalCode": "10115",
    "city": "Berlin", "country": "DE",
    "phone": "+49 30 5550000", "email": "info@…",
    "registerCourt": "Amtsgericht Berlin-Charlottenburg",
    "registerNumber": "HRB 123456",
    "vatId": "DE123456789",                 // USt-IdNr. — Hotel.VatId (Steuernummer'dan AYRI)
    "supervisoryAuthority": null,
    "disputeResolution": {                  // ODR/VSBG bildirimi
      "participatesInAdr": false,
      "noticeKey": "legal.adr.notParticipating",
      "odrPlatformUrl": "https://ec.europa.eu/consumers/odr/"
    }
  },
  "documents": [
    { "key": "terms",   "title": "Allgemeine Geschaeftsbedingungen",
      "version": "2026-07-01", "culture": "de", "bodyHtml": "…" },
    { "key": "privacy", "title": "Datenschutzerklaerung",
      "version": "2026-07-01", "culture": "de", "bodyHtml": "…" }
  ]
}
```

- `bodyHtml` **sunucuda sanitize edilmiş** (izinli etiket listesi: `p, h2, h3, ul, ol, li, a,
  strong, em, br, table…`; `script`/`style`/`iframe`/olay öznitelikleri **yasak**). İstemci
  `innerHTML` ile basar; bu yüzden sanitizasyon **sunucunun sorumluluğudur**.
- `version` string'i rezervasyon rızasında **aynen** kullanılır (§6.2).

---

## 3. Oda tipi kataloğu

### 3.1 `GET /api/v1/public/hotels/{hotelSlug}/room-types`

```jsonc
// 200 → PublicRoomTypeSummaryResponse[]   (DÜZ DİZİ)
[{
  "code": "DBL",                            // public anahtar; roomTypeId (GUID) DONMEZ
  "name": "Doppelzimmer",                   // Accept-Language'e gore cozulmus
  "shortDescription": "…",
  "capacity": 2,
  "sizeSqm": 24,
  "amenities": ["wifi", "minibar", "balcony"],
  "image": { "url": "…", "alt": "…", "width": 1200, "height": 800 },
  "fromPrice": { "amount": 120.00, "currency": "EUR", "basis": "BasePrice" }
}]
```

- `fromPrice.basis`: `"BasePrice"` — tarihsiz kataloğun "ab"-fiyatı `RoomType.BasePrice`'tır.
  Tarih verilmeden sezon fiyatı gösterilemez; PAngV açısından bu bir **"ab" fiyatıdır** ve ekranda
  öyle etiketlenmelidir ("ab 120,00 € pro Nacht"), toplam fiyat iddiası değildir.
- Katalogda **oda sayısı, doluluk, oda numarası yoktur** (mimari §4.3 yasak listesi).

### 3.2 `GET /api/v1/public/hotels/{hotelSlug}/room-types/{roomTypeCode}`

`roomTypeCode` **büyük/küçük harf duyarsızdır** (`dbl` = `DBL`).

```jsonc
// 200 → PublicRoomTypeDetailResponse
{
  "code": "DBL", "name": "Doppelzimmer",
  "shortDescription": "…",
  "description": "…",                       // uzun metin, cok dilli
  "capacity": 2, "sizeSqm": 24,
  "amenities": ["wifi", "minibar", "balcony"],
  "images": [ { "url": "…", "alt": "…", "width": 1600, "height": 900, "sortOrder": 0 } ],
  "fromPrice": { "amount": 120.00, "currency": "EUR", "basis": "BasePrice" },
  "cancellationPolicy": { /* PublicCancellationPolicy — §4.3 */ }
}
```

**404 `ROOM_TYPE_NOT_FOUND`:** kod yok veya oda tipi başka otele ait (global query filter zaten
süzer; ayrı kontrol gerekmez).

---

## 4. Müsaitlik ve fiyat teklifi

### 4.1 `GET /api/v1/public/hotels/{hotelSlug}/availability`

`?checkIn=2026-08-10&checkOut=2026-08-13&adults=2&children=0`

**Hold oluşturmaz.** Salt okuma; sayfa yenilendiğinde tekrar çağrılabilir.

```jsonc
// 200 → PublicAvailabilityResponse
{
  "hotelSlug": "berlin-mitte",
  "currency": "EUR",
  "checkIn": "2026-08-10", "checkOut": "2026-08-13", "nights": 3,
  "adults": 2, "children": 0,
  "offers": [
    {
      "roomTypeCode": "DBL",
      "name": "Doppelzimmer",
      "shortDescription": "…",
      "capacity": 2, "sizeSqm": 24,
      "amenities": ["wifi", "minibar"],
      "image": { "url": "…", "alt": "…", "width": 1200, "height": 800 },
      "availability": {
        "isAvailable": true,
        "availableUnits": 3,               // 5'te KIRPILIR
        "availableUnitsCapped": false      // true ise "5+" demektir
      },
      "price": { /* PublicPrice — §4.2 */ },
      "cancellationPolicy": { /* PublicCancellationPolicy — §4.3 */ }
    }
  ],
  "unavailableRoomTypes": [
    { "roomTypeCode": "SUI", "name": "Suite", "reason": "NoRoomAvailable" }
  ]
}
```

- `unavailableRoomTypes[].reason` ∈ `NoRoomAvailable | CapacityExceeded | MinNightsNotMet`.
  Neden döndürülüyor: misafire "başka bir tarih/kişi sayısı deneyin" demenin tek doğru yolu,
  hangi kısıtın engellediğini bilmektir. **Sayı vermez**, yalnızca sebep verir.
- `availableUnits` = o tipte, **tüm gecelerde** boş **ve** aktif hold'u olmayan oda sayısı,
  5'te kırpılmış. Kırpma doğruluğu bozmaz (UWG §5): gösterilen sayı gerçek alt sınırdır.
- Hiçbir tip müsait değilse `offers: []` döner — **404 değil**, 200. Boş sonuç bir hata değildir.

### 4.2 `PublicPrice` — PAngV nesnesi (tüm fiyat taşıyan yanıtlarda **aynı** şekil)

```jsonc
{
  "currency": "EUR",
  "totalGross": 468.00,                     // PAngV Gesamtpreis = konaklama + TUM zorunlu kalemler
  "vatIncluded": true,
  "mandatoryExtrasIncluded": true,

  "accommodationGross": 450.00,
  "accommodationNet": 420.56,
  "accommodationVat": 29.44,
  "accommodationVatRate": 7.00,             // otelin INDIRIMLI orani (TaxProfile)

  "cityTax": {
    "applies": true,
    "amount": 18.00,
    "perPersonNight": 3.00,
    "taxablePersons": 2,                    // TaxProfile.CountTaxablePersons(adults, children)
    "nights": 3,
    "vatRate": 0.00,                        // Kurtaxe KDV disidir (durchlaufender Posten)
    "includedInTotal": true,
    "chargedOnlyIfStayTakesPlace": true,    // CityTaxLiability ile TUTARLI
    "childExemptionApplied": true,
    "childAgeLimit": 18
  },

  "nightly": [
    { "date": "2026-08-10", "gross": 150.00 },
    { "date": "2026-08-11", "gross": 150.00 },
    { "date": "2026-08-12", "gross": 150.00 }
  ],
  "averageNightlyGross": 150.00,

  "depositPercent": 0.00,                   // girisde odeme -> on odeme yok
  "amountDueAtProperty": 468.00,
  "prepaidAmount": 0.00,

  "optionalExtras": []                       // bu fazda her zaman bos
}
```

**Değişmezler (test edilir):**
- `accommodationNet + accommodationVat == accommodationGross` (kuruşu kuruşuna).
- `totalGross == accommodationGross + cityTax.amount`.
- `sum(nightly[].gross) == accommodationGross`.
- `cityTax.amount == taxablePersons × nights × perPersonNight`.
- Aynı otel/tarih/oda tipi için `totalGross`, o rezervasyondan üretilen faturanın `grossAmount`
  değerine **eşittir**. Hesap `ReservationPricingService` + `InvoiceAmounts` +
  `TaxProfile.CountTaxablePersons` ile yapılır; **ikinci bir fiyat motoru yoktur**.

> **Sezon geçişi:** `nightly[]` gece gece gerçek fiyatı verir. `averageNightlyGross` yalnızca
> gösterim ortalamasıdır; ekranda "gecelik X €" olarak **tek başına** kullanılamaz — fiyat gece
> gece değişiyorsa (`nightly` değerleri eşit değilse) istemci ortalama olduğunu **belirtmek
> zorundadır** (PAngV: yanıltıcı fiyat gösterimi yasağı).

> **Kanal:** public teklif `ReservationChannel.Website` ile fiyatlanır. `Channel = Direct` olan
> fiyat planları web'e **uygulanmaz** (bkz. mimari §7.1); sırasıyla `Website` planı →
> "tüm kanallar" planı (`channel: null`) → `RoomType.BasePrice`.

### 4.3 `PublicCancellationPolicy`

```jsonc
{
  "type": "Flexible",                       // Flexible | Restricted  (bu fazda otel bazinda tek deger)
  "freeCancellationUntil": "2026-08-07T18:00:00+02:00",   // MUTLAK an, otel yerel offset'iyle
  "isFreeCancellationAvailable": true,      // simdi iptal edilirse ucretsiz mi
  "lateCancellationFeePercent": 90.00,
  "lateCancellationFeeAmount": 405.00,      // accommodationGross uzerinden; Kurtaxe HARIC
  "noShowFeePercent": 90.00,
  "noShowFeeAmount": 405.00,
  "cityTaxRefundedOnCancellation": true,    // CityTaxLiability: iptal/no-show'da Kurtaxe DOGMAZ
  "policyTextKey": "legal.cancellation.flexible"
}
```

**`freeCancellationUntil` hesabı (tek yer, `PublicCancellationService`):**
`checkIn` − `freeCancellationDaysBeforeArrival` gün → o tarihte `cutoffLocalTime` →
`Hotel.TimeZoneId` ile mutlak ana çevrilir.
- Yaz saatine geçişte **var olmayan** yerel saat (spring-forward) → **ilk geçerli sonraki** an.
- Kış saatine dönüşte **belirsiz** yerel saat (fall-back) → **daha geç** offset (misafir lehine).
- Sonuç geçmişte kalıyorsa `isFreeCancellationAvailable: false` ve `freeCancellationUntil` yine
  hesaplanmış değeri gösterir (misafir tarihin geçtiğini görebilmeli).

---

## 5. Geçici tutma (Hold)

> Neden var, süre neden 15 dakika, hangi alternatifler elendi: **mimari §5.2 / §5.3**.

### 5.1 `POST /api/v1/public/hotels/{hotelSlug}/holds`

```jsonc
// Istek — PublicCreateHoldRequest
{
  "roomTypeCode": "DBL",
  "checkIn": "2026-08-10",
  "checkOut": "2026-08-13",
  "adults": 2,
  "children": 0
}
```

```jsonc
// 201 → PublicHoldResponse   (Location: /api/v1/public/hotels/berlin-mitte/holds/{holdToken})
{
  "holdToken": "Vb3nQ8sT1kR6yPz0LmXhAw",   // 128-bit, base64url, 22 karakter
  "expiresAt": "2026-07-31T09:15:00+02:00",
  "expiresInSeconds": 900,
  "hotelSlug": "berlin-mitte",
  "roomTypeCode": "DBL",
  "checkIn": "2026-08-10", "checkOut": "2026-08-13", "nights": 3,
  "adults": 2, "children": 0,

  "price": { /* PublicPrice — DONDURULMUS */ },
  "cancellationPolicy": { /* PublicCancellationPolicy — DONDURULMUS */ },

  "orderSummary": {                         // §312j Abs. 2 BGB — BUTONUN HEMEN USTUNDE
    "essentialFeatures": {
      "roomTypeName": "Doppelzimmer",
      "roomCount": 1,
      "occupancy": { "adults": 2, "children": 0 },
      "board": "None"
    },
    "duration": {
      "checkIn": "2026-08-10", "checkOut": "2026-08-13", "nights": 3,
      "checkInFromLocal": "15:00", "checkOutUntilLocal": "11:00",
      "timeZoneId": "Europe/Berlin"
    },
    "totalPrice": {
      "amount": 468.00, "currency": "EUR",
      "vatIncluded": true, "includesMandatoryCharges": true
    },
    "components": [
      { "kind": "Accommodation", "labelKey": "summary.accommodation",
        "label": "Uebernachtung 3 Naechte", "amount": 450.00, "mandatory": true },
      { "kind": "CityTax", "labelKey": "summary.cityTax",
        "label": "Kurtaxe 2 Personen x 3 Naechte", "amount": 18.00, "mandatory": true }
    ],
    "hash": "sha256:9f2b…"                  // POST /bookings icinde AYNEN geri gonderilir
  },

  "legal": {
    "withdrawalRight": {                    // §312g Abs. 2 Nr. 9 BGB
      "applies": false,
      "legalBasis": "BGB §312g Abs. 2 Nr. 9",
      "noticeKey": "legal.withdrawal.excluded.accommodation",
      "noticeVersion": "2026-07-01"
    },
    "orderButton": {                        // §312j Abs. 3 BGB
      "labelKey": "legal.orderButton.payable",
      "labelDe": "zahlungspflichtig buchen",
      "mustBeExactLabel": true
    },
    "terms":         { "key": "terms",   "version": "2026-07-01" },
    "privacyNotice": { "key": "privacy", "version": "2026-07-01" },
    "contractConclusion": "OnConfirmationEmail"   // OnConfirmationEmail | OnHotelAcceptance
  },

  "paymentOptions": [
    { "method": "PayAtProperty", "requiresGuarantee": false }
  ],

  "requiredGuestFields": ["firstName", "lastName", "email"],
  "optionalGuestFields": ["phone", "invoiceAddress", "estimatedArrivalLocalTime", "guestNote"]
}
```

**Sunucu davranışı:**
1. Aynı transaction'da, istenen oda tipi + kesişen aralık için **süresi dolmuş hold'lar silinir**.
2. Uygun odalar arasından **deterministik** seçim: `floor` ↑, sonra `number` (doğal sıra) ↑ ilk.
3. `BookingHold` yazılır; `EX_BookingHolds_NoOverlappingActiveHolds` kısıtı yarışı çözer
   (ihlal → **409 `ROOM_NO_LONGER_AVAILABLE`**).
4. Teklif (fiyat, politika, hukuki versiyonlar, `orderSummary.hash`) hold satırına **dondurulur**;
   sonraki fiyat/plan değişiklikleri bu hold'u etkilemez.
5. **Kişisel veri yazılmaz.** Hold yalnızca hız sınırı/kötüye kullanım için tuzlanmış bir IP
   hash'i tutar ve tüketilse bile **24 saat sonra silinir**.

**`orderSummary.hash` tanımı (kesin):** `orderSummary` nesnesinin, `hash` alanı **hariç**,
anahtarları **ordinal sıralı**, boşluksuz, `InvariantCulture` sayı biçimli (`468.00`) kanonik
JSON'unun SHA-256'sı, `sha256:` önekiyle ve **küçük harf hex** olarak.

### 5.2 `GET /api/v1/public/hotels/{hotelSlug}/holds/{holdToken}`

Sayfa yenilendiğinde kalan süreyi ve **donmuş** teklifi yeniden okur. Yanıt `PublicHoldResponse`
ile **birebir aynı şekildedir** (yeni bir teklif hesaplanmaz).

- Süresi dolmuşsa → **409 `HOLD_EXPIRED`**.
- Tüketilmişse → **409 `HOLD_ALREADY_USED`** (yanıt `bookingReference` **içermez**; sorgulama
  yalnızca `accessToken` ile yapılır).
- Bulunamazsa / başka otelin token'ıysa → **404 `HOLD_NOT_FOUND`**.

### 5.3 `DELETE /api/v1/public/hotels/{hotelSlug}/holds/{holdToken}`

Misafir akıştan çıkarsa envanteri **hemen** serbest bırakır. → **204**. Bilinmeyen/süresi dolmuş
token da **204** döner (idempotent; varlık sızdırılmaz).

---

## 6. Rezervasyon oluşturma

### 6.1 `POST /api/v1/public/hotels/{hotelSlug}/bookings`

```jsonc
// Istek — PublicCreateBookingRequest
{
  "holdToken": "Vb3nQ8sT1kR6yPz0LmXhAw",

  "checkout": {                              // §312j kanit kaydi
    "summaryHash": "sha256:9f2b…",
    "orderButtonLabel": "zahlungspflichtig buchen"
  },

  "guest": {
    "firstName": "Jürgen",
    "lastName": "Müller",
    "email": "juergen.mueller@example.de",
    "phone": null,                           // opsiyonel
    "culture": "de",                         // de | en | tr — yazisma ve fatura dili
    "countryOfResidence": "DE"               // opsiyonel; Country enum ADI
  },

  "invoiceAddress": null,                    // OPSIYONEL blok; kurumsal fatura isteyenler icin
  // { "company":"…", "addressLine":"…", "postalCode":"…", "city":"…", "country":"DE", "vatId":null }

  "stay": {
    "estimatedArrivalLocalTime": "18:00",    // opsiyonel
    "guestNote": null                        // opsiyonel, <= 500
  },

  "payment": { "method": "PayAtProperty", "guarantee": null },

  "consents": {
    "termsAccepted": true,             "termsVersion": "2026-07-01",
    "privacyNoticeAcknowledged": true, "privacyNoticeVersion": "2026-07-01",
    "withdrawalNoticeAcknowledged": true, "withdrawalNoticeVersion": "2026-07-01",
    "bookerIsAdult": true,
    "marketingOptIn": false
  },

  "challengeToken": null                     // bot korumasi saglayicisinin opaque degeri (bu fazda null)
}
```

> **Kart alanı YOKTUR ve eklenmeyecektir.** Gövdede `cardNumber`, `pan`, `cvc`, `cvv`,
> `expiryMonth`, `expiryYear`, `cardholderName` adlarından biri geçerse istek **400
> `CARD_DATA_NOT_ACCEPTED`** ile reddedilir ve gövde **loglanmaz** (mimari §6.2 — PCI-DSS kapsamı
> dışında kalmanın tek yolu).

```jsonc
// 201 → PublicBookingResponse
// Location: /api/v1/public/hotels/berlin-mitte/bookings/{accessToken}
{
  "bookingReference": "K7QM-3XPD-9RTV",
  "accessToken": "hQ7pR2vK9mNc4XsA1TjW6bYdZ0f",   // YALNIZCA olusturma yanitinda doner
  "accessTokenExpiresAt": "2026-09-12T00:00:00+02:00",
  "status": "Confirmed",                     // Confirmed | InHouse | Completed | Cancelled | NoShow
  "createdAt": "2026-07-31T09:02:11+02:00",

  "hotel": {
    "slug": "berlin-mitte", "name": "HotelCore Berlin Mitte",
    "addressLine": "Chausseestrasse 1", "postalCode": "10115",
    "city": "Berlin", "country": "DE",
    "phone": "+49 30 5550000", "email": "info@…",
    "timeZoneId": "Europe/Berlin"
  },

  "stay": {
    "roomTypeCode": "DBL", "roomTypeName": "Doppelzimmer",
    "checkIn": "2026-08-10", "checkOut": "2026-08-13", "nights": 3,
    "adults": 2, "children": 0,
    "checkInFromLocal": "15:00", "checkOutUntilLocal": "11:00",
    "estimatedArrivalLocalTime": "18:00"
  },

  "guest": {
    "firstName": "Jürgen", "lastName": "Müller",
    "email": "juergen.mueller@example.de", "phone": null
  },

  "price": { /* PublicPrice — rezervasyon aninda DONDURULMUS */ },

  "cancellation": {
    /* PublicCancellationPolicy */
    "canCancelOnline": true,
    "chargedFeeAmount": null                 // iptal edilmediyse null
  },

  "payment": {
    "method": "PayAtProperty",
    "amountDueAtProperty": 468.00,
    "prepaidAmount": 0.00,
    "guarantee": null
  },

  "legal": { /* hold yanitindaki `legal` nesnesinin DONDURULMUS kopyasi */ },

  "confirmation": {
    "channel": "Email",
    "recipientMasked": "j***@e***.de",
    "sentAt": null,                          // outbox gonderdikten sonra dolar
    "documentVersion": "2026-07-01",
    "culture": "de"
  }
}
```

**`accessToken` yalnızca 201 yanıtında ve onay e-postasındaki bağlantıda görünür.** Sonraki
okumalarda (`GET .../bookings/{accessToken}`) yanıt bu alanı **taşımaz** (yanıtın loglanması/
paylaşılması hâlinde tekrar sızmasın diye).

**Sunucu davranışı (sırayla, tek transaction):**
1. Hold çözülür; süresi dolmuşsa **409 `HOLD_EXPIRED`**, tüketilmişse **409 `HOLD_ALREADY_USED`**.
2. `checkout.summaryHash` hold'daki değerle karşılaştırılır; farklıysa **409 `SUMMARY_CHANGED`**.
3. `Guest` **yeni** oluşturulur (e-postaya göre birleştirme **yapılmaz** — mimari §9.6).
   Doldurulan alanlar: `FirstName`, `LastName`, `Email`, `Phone?`, `Culture`, `Nationality: null`,
   `BirthDate: null`, adres alanları yalnızca `invoiceAddress` verildiyse.
4. `Reservation` oluşturulur: `Status = Confirmed`, `Channel = Website`, `RoomId` = hold'un
   pinlediği oda, `TotalAmount` = **hold'da donmuş** `accommodationGross`, `DepositPercent = 0`,
   `Notes` = `guestNote` varsa `"[Gast] …"` damgasıyla.
   `ReservationNumber` mevcut `RES-{yıl}-{5 hane}` üreticisiyle atanır (**misafire gösterilmez**).
5. `Folio` + `RoomCharge` satırı mevcut `ReservationFolioService.SyncRoomChargeAsync` ile yazılır.
6. `PublicBooking` yazılır: `BookingReference`, `AccessTokenHash` (SHA-256), `AccessTokenExpiresAt`
   (= `checkOut` + 30 gün), rıza alanları + versiyonlar, `OrderSummaryJson` + `SummaryHash`,
   `OrderButtonLabel`, `PriceSnapshotJson`, `CancellationPolicySnapshotJson`, `TermsVersion`.
7. Hold `ConsumedAt` + `ConsumedByReservationId` ile tüketilir.
8. Commit. **Sonra** (transaction dışında) onay e-postası outbox'a konur — gönderim hatası
   rezervasyonu geri almaz.

`EX_Reservations_NoOverlappingStays` ihlali (hold penceresi kaçmışsa) → **409
`ROOM_NO_LONGER_AVAILABLE`**.

### 6.2 Doğrulama kuralları (400 + `errors`, anahtarlar **PascalCase**)

| Alan | Kural |
|---|---|
| `holdToken` | Zorunlu; 22 karakter base64url |
| `checkout.summaryHash` | Zorunlu; `sha256:` + 64 hex |
| `checkout.orderButtonLabel` | Zorunlu, 1–120 karakter (**içeriği doğrulanmaz, kaydedilir**) |
| `guest.firstName` / `lastName` | Zorunlu, ≤ 100 (`Guest` şemasıyla aynı) |
| `guest.email` | Zorunlu, geçerli e-posta, ≤ 256 |
| `guest.phone` | Opsiyonel, ≤ 32 |
| `guest.culture` | ∈ `de \| en \| tr` |
| `guest.countryOfResidence` | Opsiyonel; `Country` enum adı |
| `invoiceAddress.addressLine` | Blok verilirse zorunlu, ≤ 256 |
| `invoiceAddress.postalCode` | ≤ 16 · `city` ≤ 100 · `company` ≤ 200 · `vatId` ≤ 32 |
| `stay.estimatedArrivalLocalTime` | Opsiyonel, `HH:mm` |
| `stay.guestNote` | Opsiyonel, ≤ 500 |
| `payment.method` | ∈ otel `paymentOptions` listesi; aksi hâlde **400 `CHANNEL_NOT_CONFIGURED`** |
| `payment.guarantee` | Bu fazda **yalnızca `null`**; `"CardGuarantee"` → **400 `CHANNEL_NOT_CONFIGURED`** |
| `consents.termsAccepted` | **`true` olmalı** (false → 400) |
| `consents.privacyNoticeAcknowledged` | **`true` olmalı** |
| `consents.withdrawalNoticeAcknowledged` | **`true` olmalı** |
| `consents.bookerIsAdult` | **`true` olmalı** |
| `consents.*Version` | Zorunlu; otelin **güncel** versiyonuyla eşleşmezse **409 `LEGAL_TEXT_CHANGED`** |
| `consents.marketingOptIn` | Opsiyonel, varsayılan `false` — **ön işaretli olamaz** (DSGVO Art. 4 Nr. 11) |

`adults`/`children`/tarih doğrulaması **hold** ucunda yapılır (§6.3); booking ucu bu değerleri
istekten **almaz**, hold'dan okur — istemcinin araya girip kişi sayısını değiştirmesi mümkün değildir.

### 6.3 Hold ucunun doğrulama kuralları

| Alan | Kural |
|---|---|
| `roomTypeCode` | Zorunlu, ≤ 10, otelde var olmalı → yoksa **404 `ROOM_TYPE_NOT_FOUND`** |
| `checkIn` | Otelin **yerel** bugününden önce olamaz (`Hotel.TimeZoneId`) |
| `checkOut` | `> checkIn` (en az 1 gece; day-use yok) |
| gece sayısı | `[booking.minNights, booking.maxNights]` aralığında |
| `checkIn` üst sınır | `≤ yerel bugün + booking.maxAdvanceDays` |
| `checkIn` alt sınır | Aynı gün rezervasyon `booking.minAdvanceHours` kadar önce kapanır |
| `adults` | 1–`booking.maxAdults` · `children` 0–`booking.maxChildren` |
| kapasite | `adults + children ≤ roomType.capacity`, aşarsa **409 `CAPACITY_EXCEEDED`** |
| müsaitlik | Uygun oda yoksa **409 `ROOM_NO_LONGER_AVAILABLE`** |

---

## 7. Rezervasyon sorgulama ve iptal

### 7.1 Referans ve token biçimi — **numaralandırma (enumeration) koruması**

| Kimlik | Biçim | Entropi | Nerede |
|---|---|---|---|
| `bookingReference` | **Crockford Base32**, 12 karakter, `4-4-4` gruplu: `K7QM-3XPD-9RTV` | **60 bit** | Ekran, e-posta, telefonda söylenir, resepsiyonda aranır |
| `accessToken` | base64url, 27 karakter | **160 bit** | Yalnızca onay e-postasındaki bağlantıda ve 201 yanıtında |

**`RES-2026-00042` public tarafta ASLA kullanılmaz.** Sıralı ve tahmin edilebilirdir; sorgulama
anahtarı yapılırsa bir saldırgan tüm rezervasyonları sırayla okur. `ReservationNumber` iç/ticari
referans olarak kalır ve misafire gösterilmez.

**Crockford Base32 neden:** alfabesi `I`, `L`, `O`, `U` harflerini içermez → `1/I`, `0/O`
karışması ve kazara küfür üretimi olmaz; telefonda hatasız dikte edilir, büyük/küçük harf
duyarsızdır. 12 karakter × 5 bit = 60 bit; hız sınırı (5/saat) ile birlikte kaba kuvvet
pratikte imkânsızdır.

**İki kimliğin rolleri ayrıdır:**
- `accessToken` bir **taşıyıcı kimlik bilgisidir**: tek başına okuma + iptal yetkisi verir.
  Veritabanında **yalnızca SHA-256 hash'i** saklanır (mevcut `RefreshToken` deseniyle aynı);
  karşılaştırma **sabit zamanlıdır**.
- `bookingReference` taşıyıcı kimlik bilgisi **değildir**: tek başına veri döndürmez. Sorgulama
  için `lookup` ucuna e-posta ile birlikte verilir ve uç **veri döndürmez**, bağlantıyı e-postayla
  gönderir (§7.4).

`accessToken` geçerlilik süresi: **`checkOut` + 30 gün**. Sonrasında **404** — verinin kendisi
GoBD/AO §147 gereği saklanmaya devam eder, yalnızca self-servis erişim kapanır.

### 7.2 `GET /api/v1/public/hotels/{hotelSlug}/bookings/{accessToken}`

`PublicBookingResponse` döner (§6.1), **`accessToken` alanı hariç**. Alanlar rezervasyon anındaki
**donmuş** değerlerdir; `status` canlıdır.

**`status` public izdüşümü** (iç `ReservationStatus` doğrudan verilmez):

| İç durum | Public `status` |
|---|---|
| `Option`, `Confirmed` | `Confirmed` |
| `CheckedIn` | `InHouse` |
| `CheckedOut` | `Completed` |
| `Cancelled` | `Cancelled` |
| `NoShow` | `NoShow` |

**404 `BOOKING_NOT_FOUND`:** token yok, süresi dolmuş, veya başka otelin token'ı — **üçü de aynı
yanıt** (gövde ve zamanlama farkı yok).

### 7.3 `POST /api/v1/public/hotels/{hotelSlug}/bookings/{accessToken}/cancel`

```jsonc
// Istek — PublicCancelBookingRequest
{
  "reason": null,                            // opsiyonel, <= 500 — rezervasyon notlarina damgali EKLENIR
  "acknowledgedFeeAmount": 405.00            // ucret dogacaksa ZORUNLU; ucretsizse null
}
```

```jsonc
// 200 → PublicBookingResponse
// status: "Cancelled", cancellation.chargedFeeAmount: 405.00 (veya 0.00)
```

**Kurallar:**
- Ücretsiz iptal penceresi içindeyse → `chargedFeeAmount: 0.00`, `acknowledgedFeeAmount`
  gönderilmemelidir (gönderilirse ve `0.00` değilse **400**).
- Pencere kapandıysa iptal **yine mümkündür**, ama ücret doğar. `acknowledgedFeeAmount` yoksa veya
  sunucunun hesabıyla **eşleşmiyorsa** → **409 `FEE_ACKNOWLEDGEMENT_REQUIRED`**, yanıt doğru tutarı
  `errors: { "AcknowledgedFeeAmount": [...] }` içinde bildirir. Amaç: misafirin ücreti görmeden
  iptal etmesini engellemek.
- Ücret matrahı **yalnızca konaklama tutarıdır** (`accommodationGross`); **Kurtaxe girmez**
  (`CityTaxLiability`: konaklama gerçekleşmediği için vergi doğmaz).
- `InHouse` / `Completed` durumunda online iptal **yoktur** → **409 `CANCELLATION_NOT_ALLOWED`**
  (misafir oteli aramalıdır; `detail` bunu söyler).
- Zaten iptalliyse → **409 `BOOKING_ALREADY_CANCELLED`** (idempotent değil; çift tıklamayı istemci
  engeller).
- İç etki: `Reservation.Status = Cancelled` (mevcut durum makinesi üzerinden). **Ücret tahsilatı
  ve faturalama bu uçta yapılmaz** — otelin mevcut faturalama akışına bırakılır; public sözleşme
  yalnızca tutarı **bildirir**. (İptal bedelinin KDV'si açık bir sorudur: README ve mimari §10.6.)

### 7.4 `POST /api/v1/public/hotels/{hotelSlug}/bookings/lookup`

Bağlantısını kaybeden misafir için. **Hiçbir koşulda veri döndürmez.**

```jsonc
// Istek — PublicBookingLookupRequest
{ "bookingReference": "K7QM-3XPD-9RTV", "email": "juergen.mueller@example.de" }
```

```jsonc
// 202 Accepted — GOVDE YOK
```

- Eşleşme **varsa**: erişim bağlantısını içeren e-posta gönderilir. **Bağlantı YENİDİR ve
  öncekini geçersiz kılar.** Sunucu ham token'ı saklamaz (yalnızca SHA-256 özeti), dolayısıyla
  eskisini yeniden gönderemez; yeni bir token üretip özetini yazar. Misafirin elindeki eski
  bağlantı bundan sonra **404** verir — bu bir hata değil, hash-only saklamanın kaçınılmaz
  sonucudur ve istemci "yeni bağlantı gönderildi" derken bunu **söylemelidir**.
- Eşleşme **yoksa**: hiçbir şey yapılmaz.
- **Her iki durumda da 202** ve **aynı gecikme profili** (sabit minimum işlem süresi) — böylece
  ne yanıt gövdesi ne yanıt süresi bir rezervasyonun varlığını sızdırır.
- `bookingReference` biçimi normalize edilir: büyük harfe çevrilir, `-` ve boşluk atılır,
  Crockford eşlemesi (`I→1`, `L→1`, `O→0`) uygulanır. Geçersiz biçim de **202** döner.
- Hız sınırı **5/saat** (IP) ve **3/saat** (e-posta hash'i).

---

## 8. Hata kodları — tam katalog

Tüm yanıtlar `ProblemDetails` + `extensions.code`.

| Status | `code` | Ne zaman |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Genel doğrulama; `errors` doludur |
| 400 | `CARD_DATA_NOT_ACCEPTED` | Gövdede kart alanı adı geçti (§6.1 tripwire) |
| 400 | `CHANNEL_NOT_CONFIGURED` | Desteklenmeyen ödeme yöntemi/garanti istendi |
| 404 | `BRAND_NOT_FOUND` | Marka slug'ı yok veya public oteli yok |
| 404 | `HOTEL_NOT_FOUND` | Slug yok / silinmiş / public kanal kapalı (**ayırt edilmez**) |
| 404 | `ROOM_TYPE_NOT_FOUND` | Kod yok veya başka otelde |
| 404 | `HOLD_NOT_FOUND` | Token yok veya başka otelin |
| 404 | `BOOKING_NOT_FOUND` | Token yok / süresi dolmuş / başka otelin (**ayırt edilmez**) |
| 409 | `HOLD_EXPIRED` | Hold'un 15 dakikası doldu |
| 409 | `HOLD_ALREADY_USED` | Hold zaten bir rezervasyona dönüştü |
| 409 | `ROOM_NO_LONGER_AVAILABLE` | Uygun oda kalmadı (hold veya `EXCLUDE` kısıtı) |
| 409 | `CAPACITY_EXCEEDED` | `adults + children > roomType.capacity` |
| 409 | `SUMMARY_CHANGED` | `checkout.summaryHash` hold'daki özetle uyuşmuyor (§312j Abs. 2) |
| 409 | `LEGAL_TEXT_CHANGED` | Onaylanan AGB/aydınlatma versiyonu artık güncel değil |
| 409 | `CANCELLATION_NOT_ALLOWED` | Giriş yapılmış / tamamlanmış konaklama |
| 409 | `FEE_ACKNOWLEDGEMENT_REQUIRED` | Ücretli iptalde tutar teyidi eksik/yanlış |
| 409 | `BOOKING_ALREADY_CANCELLED` | Rezervasyon zaten iptal |
| 429 | `RATE_LIMIT_EXCEEDED` | Hız sınırı; `Retry-After` başlığı zorunlu |
| 503 | `PAYMENT_PROVIDER_UNAVAILABLE` | PSP takıldıktan sonra; bu fazda üretilmez |

> **Public uçlar 401 ve 403 ÜRETMEZ.** 403, sorulan kaynağın var olduğunu doğrular; public tarafta
> her yetki/varlık sorunu **404**'e indirgenir (admin tarafındaki "erişilemeyen otel 404" kararıyla
> aynı ilke).

---

## 9. Hukuki eşleme tablosu — hangi kural, hangi uç/alan/ekran

| Kural | Uç | Alan | Ekran | Zorlama |
|---|---|---|---|---|
| **§312j Abs. 3 BGB** (Button-Lösung) | `POST /holds` → `POST /bookings` | `legal.orderButton.labelDe` = `zahlungspflichtig buchen`, `mustBeExactLabel`; `checkout.orderButtonLabel` (kanıt kaydı) | Özet/ödeme sayfasındaki tek birincil düğme | Sunucu metni **doğrulayamaz**, **dondurur**. i18n anahtarı otel bazında değiştirilemez |
| **§312j Abs. 2 BGB** (düğme üstü zorunlu özet) | `POST /holds` | `orderSummary.{essentialFeatures,duration,totalPrice,components}` + `hash` | Düğmenin **hemen üstünde**, açılır/kapanır olmayan blok | `summaryHash` uyuşmazsa **409 `SUMMARY_CHANGED`** |
| **PAngV** (KDV dâhil toplam + zorunlu ek kalemler) | Fiyat taşıyan tüm uçlar | `price.totalGross`, `vatIncluded`, `mandatoryExtrasIncluded`, `cityTax.includedInTotal` | Arama sonucu, detay, özet, onay | Değişmez testleri (§4.2); Kurtaxe toplama **dâhil** |
| **PAngV** ("ab" fiyatı) | `/room-types` | `fromPrice.basis = "BasePrice"` | Katalog kartı | Etiket "ab …" olmalı; toplam fiyat iddiası değil |
| **§312g Abs. 2 Nr. 9 BGB** (cayma hakkı **yok**) | `POST /holds`, `GET /bookings/{token}` | `legal.withdrawalRight.{applies:false, legalBasis, noticeKey, noticeVersion}` | Özet sayfasında ayrı kutu; onay e-postasında | Genel Widerrufsbelehrung **gösterilmez**; onay + versiyon dondurulur |
| **§5 DDG** (Impressum) | `GET /legal` | `imprint.*` (13 alan) | Prerender `/impressum`, her sayfanın altbilgisinde bağlantı | Tümü DB'den; hardcode yok. JS'siz erişilebilir |
| **DSGVO Art. 13** (aydınlatma) | `GET /legal` | `documents[key="privacy"]` + `version` | `/datenschutz` + rezervasyon formunda bağlantı | `consents.privacyNoticeVersion` kaydedilir (Art. 7 Abs. 1) |
| **§25 TDDDG** (çerez onayı) | — (API çerez **koymaz**) | — | Onay bandı; onay gelene kadar üçüncü taraf script **DOM'a eklenmez** | Yalnızca zorunlu depolama (`holdToken`, dil) — §25 Abs. 2 Nr. 2 istisnası, gizlilik metninde yazılı |
| **§312f BGB** (kalıcı veri taşıyıcısı) | `POST /bookings` → outbox | `confirmation.{channel,sentAt,documentVersion,culture}`; `PublicBooking.ConfirmationDocumentHash` | Onay e-postası (içerik **gövdede**) | Zorunlu içerik listesi: mimari §9.8 |
| **DSGVO veri minimizasyonu** | `POST /bookings` | `requiredGuestFields` = `[firstName, lastName, email]`; doğum tarihi/uyrukluk/kimlik **alan olarak yok** | Rezervasyon formu | Meldeschein (BMG §§29–30) verisi **girişte**, admin tarafında alınır |
| **Kurtaxe tutarlılığı** | Fiyat taşıyan tüm uçlar | `cityTax.chargedOnlyIfStayTakesPlace`, `cancellation.cityTaxRefundedOnCancellation`, `appliesToAccommodationOnly` | Fiyat kırılımı, iptal ekranı | `CityTaxLiability.ArisesFrom` ile **aynı** kural; iptal ücreti matrahına girmez |
| **Kurtaxe çocuk muafiyeti** | Fiyat taşıyan tüm uçlar | `cityTax.{taxablePersons,childExemptionApplied,childAgeLimit}` | Fiyat kırılımı dipnotu | `TaxProfile.CountTaxablePersons` — yeniden hesaplanmaz |
| **UWG §5** (yanıltıcı kıtlık iddiası) | `/availability` | `availableUnits` (5'te kırpılı), `availableUnitsCapped` | "Nur noch N Zimmer" rozeti | Kırpılmış değer **gerçek**; uydurma sayı yok |
| **DSGVO Art. 4 Nr. 11** (onay serbestliği) | `POST /bookings` | `consents.marketingOptIn` varsayılan `false` | Onay kutusu **ön işaretli olamaz** | Rezervasyon `marketingOptIn: false` ile de tamamlanır |
| **PCI-DSS kapsam dışılığı** | Tüm public uçlar | Kart alanı **yok**; tripwire | PSP iframe/SDK (bu fazda yok) | `CARD_DATA_NOT_ACCEPTED` + gövde loglanmaz |

---

## 10. Admin tarafındaki eklemeler (aynı sözleşmenin parçası)

**Yeni izin anahtarı yoktur.**

| Method | Path | İzin | Değişiklik |
|---|---|---|---|
| GET/PUT | `/api/v1/hotels/{id}/settings` | `Settings.Manage` | Gövdeye **`publicBooking`**, **`cancellationPolicy`**, **`legalProfile`** blokları eklenir; `taxProfile` gibi GET gövdesi doğrudan PUT'a gönderilebilir kalır |
| GET | `/api/v1/reservations/{id}` | `Reservations.View` | Yanıta `publicReference` (`string?`) ve `channel: "Website"` |
| GET | `/api/v1/reservations/{id}/public-booking` | `Reservations.View` | **Yeni.** Rıza ve hukuki anlık görüntü: onaylanan versiyonlar, gösterilen düğme metni, `orderSummary`, fiyat/politika snapshot'ı, onay e-postası kaydı. Uyuşmazlıkta otelin kanıtı |

`PUT /hotels/{id}/settings` doğrulaması (ek):
`publicBooking.slug` zorunlu (kanal açıksa), `^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$`, **global
benzersiz** → çakışma **409** · `timeZoneId` geçerli IANA kimliği · `minNights` 1–30 ·
`maxNights` `≥ minNights`, ≤ 365 · `maxAdvanceDays` 1–730 · `minAdvanceHours` 0–72 ·
`cancellationPolicy.freeCancellationDaysBeforeArrival` 0–90 ·
`lateCancellationFeePercent` / `noShowFeePercent` 0–100 ·
`legalProfile.legalEntityName` zorunlu ≤ 200 (kanal açıksa) · `vatId` ≤ 32.

> **Kanal açılırken uyarı (409 değil, yanıt uyarısı):** otelin `Website` veya "tüm kanallar"
> (`channel: null`) fiyat planı yoksa fiyat `RoomType.BasePrice`'a düşer. Ayarlar yanıtı
> `warnings: ["NoRatePlanForWebsiteChannel"]` ile bunu bildirir (bkz. mimari §7.1).

---

## 11. Frontend client üretimi (misafir uygulaması)

```bash
cd src/frontend
npx ng-openapi-gen \
  --input http://localhost:5080/swagger/public-v1/swagger.json \
  --output projects/shared/src/public-api-types
```

> Misafir uygulaması **admin şemasından client üretmez**; iki belge ayrıdır (mimari §3).
> Üretilen tipler paylaşılan kütüphanede yaşar, ama kütüphaneye **JWT'ye dokunan hiçbir şey
> girmez** (mimari §2.1).

---

## 12. Bu sözleşmede bilinçli olarak **olmayanlar**

- Çoklu oda / grup rezervasyonu (`ReservationGroup` ayrı faz).
- Pansiyon (board) seçimi; `orderSummary.essentialFeatures.board` sabit `"None"`.
- Promosyon/kampanya kodu.
- Fiyat planı bazlı iptal politikası ("non-refundable" tarife).
- Ön ödeme / gerçek PSP akışı (`payment.guarantee` yalnızca `null`).
- Misafir hesabı / giriş / rezervasyon geçmişi (token ile erişim yeterli, kimlik yönetimi
  gerektirmez — veri minimizasyonu açısından da tercih edilir).
- DSGVO Art. 17 self-servis silme ucu (mimari §10.8).

---

## 13. Sözleşme ile **gerçekleşen kod** arasındaki farklar

Bu bölüm uçtan uca entegrasyondan sonra yazıldı: iki taraf birbirini görmeden geliştirildi, uçlar
canlı API'ye bağlandı ve gerçek tarayıcıyla tam bir rezervasyon yapıldı. **Belge gerçeği anlatır;**
aşağıdakiler ya sözleşmeye eklendi ya da düzeltildi. Kalanlar bilinçli kabullerdir.

### 13.1 Backend tarafının bildirdiği belge–şema çelişkileri

| # | Konu | Sözleşme ne diyor | Kod ne yapıyor | Karar |
|---|---|---|---|---|
| B1 | `hotel.description` | §2.2 alanı **zorunlu** gösterir | `Hotel` tablosunda açıklama kolonu **yok**; metin yalnızca `Translation` (`Hotel.Description`) tablosundan okunur, kayıt yoksa `null` | Alan **nullable**'dır. Demo veride kayıt olmadığı için `null` döner |
| B2 | `shortDescription` | §3.1 ayrı alan ister | Ayrı "kısa açıklama" kolonu yok; uzun metinden **cümle sınırında** türetilir | Türetme kuralı korunur; kolon eklemek ayrı bir şema kararıdır |
| B3 | `image.alt/width/height` | §2.2 örneği hepsini dolu gösterir | Kolonlar **nullable**; yanıt da nullable taşır | Bugün hiçbir yol dimensionsuz görsel üretmiyor (yükleme boru hattı yok). İstemci `null` görseli **yer tutucuya** düşürür |
| B4 | `imprint.disputeResolution` | §2.3 `participatesInAdr`, `noticeKey`, `odrPlatformUrl` | Ek olarak **`notice`** (otelin kendi VSBG metni) döner | Ek alan sözleşmeye **eklendi**: §36 VSBG metni otel bazında değişir, sabit bir i18n anahtarı yetmez |
| B5 | `legal.documents[]` | §2.3 örneği `terms` + `privacy` | Üçüncü belge **`withdrawal`** de döner (§312g bildirimi, versiyonu rızada kullanılır) | Sözleşmeye **eklendi**; `documents[]` açık uçlu bir listedir |
| B6 | `confirmation.documentVersion` | §6.1 örneği `"2026-07-01"` | `PublicChannel:ConfirmationDocumentVersion` ayarından gelir (varsayılan `"1"`) | Bu **onay e-postası şablonunun** versiyonudur, hukuki metnin değil. Örnek yanıltıcıydı |
| B7 | Ödeme garantisi | §6.2 `guarantee: "CardGuarantee"` → 400 | Sağlayıcı yapılandırılmadığı için istek **reddedilir**, sessizce yok sayılmaz | Sözleşmeyle aynı; davranış açıkça test edilir |
| B8 | Hız sınırı eşikleri | §1.2 tablosu | Tümü `appsettings > PublicChannel:RateLimits` içinde; **kodda sabit yok**. Tanımsız bir uca sınır **uygulanmaz** | Tablo "varsayılan"dır; belge bunu zaten söylüyor |
| B9 | Kimlik bastırma | §1 "`Authorization` yok sayılır" | Public yolda kimlik **tamamen bastırılır**; çözülemeyen slug'da kapsam **boş** kalır (admin token'lı istek kendi oteline düşemez) | Sözleşmeden daha katı; belge §1'e not olarak eklendi |

### 13.2 Frontend tarafının bildirdiği sapmalar

| # | Konu | Mimari/sözleşme ne diyor | Uygulama ne yapıyor | Karar |
|---|---|---|---|---|
| F1 | **Rota şeması** | Mimari §2.2: `/{lang}/{hotelSlug}/zimmer/{code}`, `/suche`, `/buchen`, `/buchung/{token}`, `/impressum` | `/{lang}/rooms/{code}`, `/search`, `/booking`, `/confirmation/{token}`, `/manage/{token}`, `/legal/{imprint\|privacy\|terms}` — **`hotelSlug` yolda yok** | **Uygulama esas alındı.** Bu tur otel-başına-alan-adı dağıtımını hedefler; slug yapılandırmadan gelir ve **API çağrısında** yolda durur. Mimari §2.2 tablosu gerçek rotalarla güncellendi |
| F2 | Oda tipi detay sayfası | Mimari §2.2: **prerender** | **SSR** (istek anında) | Fiyat ve müsaitlik canlı veridir; bir hafta önceki fiyatı gösteren prerender sayfa PAngV açısından yanlış olurdu. **Aynı gerekçe ana sayfa için de uygulandı** — bkz. §13.3-D10 |
| F3 | Sipariş düğmesi metni | §312j Abs. 3: sunucu metni **doğrulamaz**, dondurur | İstemci sunucunun verdiği metni **aynen** render eder ve **aynen** geri gönderir; CSS `text-transform` bu düğmede kapalıdır | Sözleşmeyle aynı; büyük harfe çeviren bir CSS kuralı kanıtı bozardı |
| F4 | Özet kalem etiketleri | §5.1 örneği yerelleştirilmiş etiket gösterir | İstemcide `labelKey` çevirisi varsa onu, yoksa sunucunun `label`'ını basar | **Sunucu düzeltildi** (§13.3-D1); istemci kataloğunda bu anahtarlar yoktur, dolayısıyla ekranda ve kanıtta **aynı** metin durur |
| F5 | Fiyat değiştiğinde | §5.2 donmuş teklif | Yenilenen teklifte tutar değiştiyse akış **durur**, eski/yeni tutar yan yana gösterilir ve yeniden onay istenir | Sözleşmeden daha katı; §312j Abs. 2'nin amacı budur |
| F6 | Onay/sorgulama sayfaları | Mimari §2.2 CSR | CSR + `X-Robots-Tag: noindex, nofollow` **HTTP başlığı** olarak da gönderilir | Ek güvence; belgeye eklendi |

### 13.3 Entegrasyonda bulunan gerçek uyuşmazlıklar (bu turda düzeltildi)

| # | Bulgu | Hangi taraf hatalıydı ve neden | Düzeltme |
|---|---|---|---|
| D1 | §312j Abs. 2 zorunlu özetinin kalem etiketleri Almanca akışta **İngilizce** yazıyordu (`City tax · 2 × 3 night(s)`) ve aynı metin kanıt olarak donuyordu | **Backend.** §5.1 örneği yerelleştirilmiş etiket gösterir; ayrıca zorunlu özet sözleşmenin kurulduğu dilde olmak zorundadır ve **gösterilen** ile **saklanan** metin ayrışamaz | Etiketler `Messages` üzerinden isteğin dilinde üretilir (tekil/çoğul dahil) ve hold'a o dilde donar. Hash donmuş özetten okunduğu için dil değişimi `SUMMARY_CHANGED` üretmez |
| D2 | Onay e-postasındaki erişim bağlantısı `http://localhost:4200/{culture}/{hotelSlug}/buchung/{token}` idi: **yanlış port** (4200 yönetim panelidir) ve **var olmayan rota** | **Backend yapılandırması.** Rota tablosunun sahibi istemcidir; şablon ona uymak zorundadır | `AccessLinkTemplate` → `http://localhost:4300/{culture}/manage/{accessToken}`; ayarın yanına "misafir uygulamasının rota tablosuyla birebir aynı olmalı" notu yazıldı |
| D3 | Yönetim panelinde `publicReference` **hiçbir ekranda görünmüyordu** | **Frontend (admin).** §10 alanı yanıta ekliyor; misafirin elinde yalnızca bu referans var, `reservationNumber` ona hiç verilmiyor | Rezervasyon detayına eklendi (`ReservationResponse.publicReference` modele de eklendi) |
| D4 | Rezervasyon **araması** public referansı kapsamıyordu: resepsiyon, misafirin telefonda okuduğu numarayla kaydı bulamıyordu | **Backend.** Katkısız bir eksik: alan yanıtta vardı, aranabilir değildi | `search` artık `PublicBooking.BookingReference`'ı da kapsar; terim `lookup` ucundakiyle **aynı** normalizasyondan geçer (tire/boşluk atılır, Crockford `I→1, L→1, O→0`) |
| D5 | `ReservationChannel.Website` yönetim panelinin kanal listesinde **yoktu**: web rezervasyonlarında kanal etiketi boş kalıyordu ve **fiyat planı formunda `Website` seçilemiyordu** | **Frontend (admin).** Fiyat seçimi kanalı birebir karşılaştırdığı için (mimari §7.1) bu, "web planı hiç oluşturulamaz" demekti | Kanal listesine + etiket sözlüğüne + üç dilin kataloğuna eklendi |
| D6 | Prerender edilen hukuki sayfalar **boştu** (JavaScript'siz ziyaretçide Impressum yok) | **Frontend.** §5 DDG "unmittelbar erreichbar" ister; içerik istemcide dolduruluyordu | Derleme öncesi alınan bir anlık görüntü (`npm run legal:snapshot`) prerender sırasında `GET /legal` yanıtını karşılar; CI üretilen HTML'de künyeyi arar (bkz. §13.4) |
| D7 | Yönetim paneli geliştirme ortamı `apiBaseUrl` olarak **mutlak** adres kullanıyordu; kendi yorumunda "vekil sayesinde CORS gerekmez" yazmasına rağmen vekil hiç devreye girmiyordu | **Frontend (admin).** Sonuç: 4200 dışındaki bir portta panel "sunucuya ulaşılamıyor" veriyordu (CORS listesi tek origin) | `apiBaseUrl` göreli (`/api/v1`) yapıldı; hedef backend artık `--proxy-config` ile değiştirilebilir |
| D8 | Türkçe kaynak metinlerinde diakritik kaybı (`Cok fazla istek`, `Saat dilimi taninmiyor...`) | **Backend.** Üç dilin de aynı özenle yazılması sözleşmenin i18n kuralıdır | Düzeltildi; 429 başlığı üç dilde de nokta ile biter (diğer başlıklarla tutarlı) |
| D9 | `POST /bookings/lookup` **erişim token'ını döndürüyor** (rotate) — belge bunu söylemiyordu | Kusur değil, **belge eksiği**: ham token saklanmaz, yalnızca SHA-256 özeti; dolayısıyla sunucu eski bağlantıyı yeniden gönderemez, **yeni** bir bağlantı üretmek zorundadır | §7.4'e yazıldı: lookup **önceki bağlantıyı geçersiz kılar** |
| D10 | **Ana sayfa prerender ediliyordu ama katalogsuz üretiliyordu**: derleme `Unable to handle request: /hotels/{slug}` ve `/room-types` yazıyor, çıkış kodu 0 kalıyordu; dağıtılan HTML'de tek bir oda adı ya da "ab" fiyatı yoktu | **Frontend — ve asıl mesele eksik yapılandırma değil, kuralın kendi içinde çelişmesiydi.** Ana sayfa "nadiren değişir" diye prerender'a konmuştu, ama kartları "ab" fiyatı taşıyor; oda tipi detayını SSR'a koyarken kullandığımız gerekçe (*"önceden üretilmiş sayfa geçen haftanın fiyatını gösterir"*) burada da geçerliydi. Anlık görüntüyü kataloğu kapsayacak şekilde genişletmek **reddedildi**: depoda bayatlayan bir fiyat, PAngV/UWG açısından yanlış bir **fiyat iddiasıdır**; hukuki metnin bayatlaması ise eski ama *yayımlanmış* bir belgedir | Ana sayfa **SSR**'a alındı; prerender yalnızca hukuki sayfalarda kaldı (12 → **9** sayfa). Artık `/de` HTML'i sunucudan üç oda tipi adı ve üç "ab" fiyatıyla geliyor. Ayrıca **iki derleme kapısı** eklendi: düşen prerender isteği derlemeyi kırar ve `npm run verify:build` prerender kümesini + içeriğini + SSR çıktısını denetler (mimari §2.3) |
| D11 | Seed `/assets/demo/...` yollarına işaret ediyordu, **dosyalar yoktu**: sayfa başına ~14 adet 404 | **Seed/varlık eksiği.** Arayüz kırık görsel göstermiyordu (yer tutucu doğru), ama bir demoda 404 selini gerçek bir entegrasyon hatasından ayırmak mümkün değil — "kırık kırık görünsün" ilkesinin tersi | Arayüzün **kendi yer tutucu diliyle** (kâğıt zemin, 1px cetvel, çapraz iki çizgi, mono etiket) doğru ölçülerde 10 SVG üretildi ve seed'in işaret ettiği yollara kondu; seed URL'leri `.svg` oldu. Fotoğraf taklidi yok — ama `width`/`height` (CLS) ve `alt` (WCAG + i18n) yolları artık uçtan uca çalışıyor. Seed'in demo görsel kümesi **kendini onarır** (kümeden çıkan satırlar silinir), yoksa uzantı değişimi galeriyi ikiye katlardı |

### 13.4 Doğrulanmış davranışlar (uçtan uca, gerçek tarayıcı + gerçek API)

- **Fiyat zinciri kuruşu kuruşuna:** arama sonucu `438,00 €` = oda detayı = `orderSummary.totalPrice`
  = `POST /bookings` yanıtı `price.totalGross` = üretilen faturanın `grossAmount` (`438.00`;
  `netAmount 389.72` + `vatAmount 27.28` + `cityTaxAmount 21.00`). Kurtaxe ekranda `21,00 €`,
  faturada `CityTax` satırı `21.00`.
- **Hold gerçekten tavsiye niteliğinde:** hold alınmış oda yönetim panelinin müsaitlik ekranında
  **müsait görünür** ve aynı tarihe **satılabilir**; misafir hold'unu rezervasyona çevirmek
  istediğinde `EX_Reservations_NoOverlappingStays` devreye girer ve
  **409 `ROOM_NO_LONGER_AVAILABLE`** döner. Misafir tarafı `availableUnits` sayısını hold kadar
  düşürür (5 → 4), admin tarafı düşürmez.
- **Üç dil:** `Content-Language` isteğe göre döner; `detail` metinleri ve `extensions.code`
  ayrışmaz; hukuki belgeler istenen dilde yoksa otelin varsayılan diline düşer (TR'de AGB Almanca
  gelir, sürüm etiketi Türkçedir).
- **429:** `Retry-After: 60` + `code: RATE_LIMIT_EXCEEDED`; `detail` hangi eşiğin aşıldığını
  söylemez.
- **Kart tuzak teli:** gövdede `cardNumber` → 400 `CARD_DATA_NOT_ACCEPTED`, gövde loglanmaz.
- **`lookup`:** eşleşme olsa da olmasa da **202** ve aynı gecikme profili (ölçüldü: 0,44 s / 0,41 s).
  İki dal da denendi: eşleşen e-postada önceki erişim bağlantısı gerçekten **ölüyor** (sonraki
  okuma 404), eşleşmeyen e-postada hiçbir şey olmuyor ve eski bağlantı çalışmaya devam ediyor.
- **Sunucudan gelen ana sayfa HTML'i** (JavaScript çalışmadan): üç oda tipi adı
  (`Doppelzimmer`, `Einzelzimmer`, `Suite`) ve üç "ab" fiyatı (`129,00`, `89,00`, `219,00`)
  işaretlemenin içinde. Görseller yükleniyor: **0 yer tutucu, 0 kırık görsel, 0 başarısız istek.**
