# HotelCore — API Sözleşmesi: Rezervasyon Modülü

> Bu dosya **`docs/api-contracts.md`'ye taşınmak üzere** hazırlanmış rezervasyon modülü
> bölümüdür (Main Agent birleştirir). Biçim, ana dosyadaki "Personel" bölümünü taklit eder.
> **Kaynak-of-truth** yine backend'in ürettiği OpenAPI şemasıdır (`/swagger/v1/swagger.json`).
> Genel kurallar (base URL, `Authorization`, `X-Hotel-Id`, `Accept-Language`, sayfalama,
> `ProblemDetails`) ana dosyadaki "Genel Kurallar" bölümünde geçerlidir.

Kapsanan modüller: **Guests** (misafir), **Rate Plans** (fiyat planı),
**Availability & Occupancy** (müsaitlik + doluluk grid'i), **Reservations** (rezervasyon + folio).

---

## Temel karar: yarı açık tarih aralığı `[CheckIn, CheckOut)`

Tüm modülde konaklama aralığı **yarı açıktır**: `checkIn` **dahil**, `checkOut` **dahil değil**.

- Gece sayısı `nights = checkOut - checkIn`; **çıkış günü için ücret alınmaz**.
- İki rezervasyon çakışır ⇔ `mevcut.checkIn < istenen.checkOut && istenen.checkIn < mevcut.checkOut`
  (uç noktalarda eşitlik **çakışma değildir**).
- Somut sonucu: bir misafirin **çıkış günü**, aynı odada başka bir rezervasyonun **giriş günü**
  olabilir (sabah çıkış / öğleden sonra giriş). Bu ardışık satış **serbesttir**.
- `Cancelled` ve `NoShow` rezervasyonlar oda takvimini **bloke etmez**; `isOutOfOrder` odalar
  hiçbir tarihte müsait **sayılmaz**.

Kural tek yerde: `Application/Common/Interfaces/IAvailabilityService.cs` +
`Application/Common/Services/{AvailabilityService,AvailabilityQuery}.cs`.

> **Fiyat planı aralığı farklıdır:** `RatePlan.validFrom`/`validTo` **kapalı** aralıktır
> (`validTo` dahil), çünkü plan bir *gün kümesi* tanımlar, konaklama ise *gece kümesi*.

---

## Guests (Misafir)

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/guests` | `Reservations.View` | Sayfalı + arama |
| GET | `/guests/{id}` | `Reservations.View` | Yanıtta `stayCount` (sunucuda hesaplanır) |
| POST | `/guests` | `Reservations.Create` | 201 + `Location` |
| PUT | `/guests/{id}` | `Reservations.Create` | Tam güncelleme |
| DELETE | `/guests/{id}` | `Reservations.Create` | Soft-delete; aktif/gelecek rezervasyon varsa **409** |

> **İzin şeması bilinçli:** misafir verisi rezervasyon modülünün parçasıdır; resepsiyon
> rezervasyon alırken misafir kaydını da açar. Bu yüzden ayrı bir `Guests.*` izin anahtarı
> **tanımlanmadı** (architecture.md §7 izin listesi genişletilmedi).

```jsonc
// GuestResponse
{ "id":"guid", "firstName":"Jürgen", "lastName":"Müller", "fullName":"Jürgen Müller",
  "email":"juergen.mueller@example.de", "phone":"+49 30 5551234",
  "nationality":"DE",                       // Country enum ADI (string) veya null
  "addressLine":"Schönhauser Allee 12", "postalCode":"10435", "city":"Berlin",
  "birthDate":null, "culture":"de", "note":"Späte Anreise",
  "stayCount":1 }                           // detay/yazma yanitlarinda; LISTEDE null

// GET /guests → PagedResult<GuestResponse>
// Filtreler: ?page=1&pageSize=20&search=
//   search  -> ad, soyad ve e-postada contains (case-insensitive)
//   siralama: lastName, firstName

// POST/PUT /guests
{ "firstName":"Jürgen", "lastName":"Müller", "email":null, "phone":null,
  "nationality":"DE", "addressLine":null, "postalCode":null, "city":null,
  "birthDate":null, "culture":"de", "note":null }
```

**`stayCount` tanımı:** misafirin **`CheckedOut`** durumundaki rezervasyon sayısı (tamamlanmış
konaklamalar). İptal/no-show ve gelecekteki rezervasyonlar sayılmaz. Entity'de kolon olarak
tutulmaz (architecture.md §4.3), her istekte hesaplanır. Liste yanıtında satır başına korele
alt sorgu maliyetinden kaçınmak için `null` döner.

**Doğrulama:** `firstName`/`lastName` zorunlu ≤ 100 · `email` geçerli ≤ 256 · `phone` ≤ 32 ·
`addressLine` ≤ 256 · `postalCode` ≤ 16 · `city` ≤ 100 · `note` ≤ 1000 ·
`nationality` `Country` enum adı (`DE|AT|CH|TR|...`) veya null · `culture` ∈ `de|en|tr` ·
`birthDate` gelecekte olamaz.

**Silme kuralı (409):** misafirin `CheckedIn` bir rezervasyonu varsa **veya** `checkOut >= bugün`
olan, iptal/no-show/çıkış yapılmamış bir rezervasyonu varsa silinemez. Geçmiş konaklamalar
engel değildir; kayıt soft-delete olduğu için tarihçe (ve FK'ler) korunur.

> **Misafirde benzersizlik kuralı YOKTUR:** aynı isim/e-posta ile birden çok kayıt meşrudur
> (aynı adı taşıyan farklı kişiler, ailenin ortak e-postası). Tekilleştirme (merge) ileride
> ayrı bir use-case olacaktır; sessizce mevcut kayda bağlanmak yanlış misafire konaklama
> yazma riski taşır.

---

## Rate Plans (Fiyat planı)

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/rate-plans` | `Rates.View` | Düz dizi (plan sayısı az, sayfalama yok) |
| GET | `/rate-plans/{id}` | `Rates.View` | |
| POST | `/rate-plans` | `Rates.Manage` | 201 + `Location`; çakışma **409** |
| PUT | `/rate-plans/{id}` | `Rates.Manage` | Çakışma kontrolü kendisi hariç tutularak |
| DELETE | `/rate-plans/{id}` | `Rates.Manage` | **Hard delete**; plana bağlı rezervasyon varsa **409** |

```jsonc
// RatePlanResponse
{ "id":"guid", "roomTypeId":"guid", "roomTypeCode":"DBL", "roomTypeName":"Doppelzimmer",
  "name":"Sommer BAR Doppelzimmer", "price":150.00, "currency":"EUR",
  "validFrom":"2026-08-01", "validTo":"2026-08-31",   // KAPALI aralik (validTo dahil)
  "channel":null,                                     // ReservationChannel ADI veya null = tum kanallar
  "isActive":true }

// GET /rate-plans?roomTypeId=&date=
//   date -> o gun gecerli planlar (validFrom <= date <= validTo)
//   siralama: validFrom, name

// POST/PUT /rate-plans
{ "roomTypeId":"guid", "name":"Sommer BAR", "price":150.00,
  "validFrom":"2026-08-01", "validTo":"2026-08-31",
  "channel":"BookingCom",     // opsiyonel; null/verilmezse tum kanallar
  "isActive":true }           // opsiyonel, varsayilan true
```

**Doğrulama:** `roomTypeId` **aynı otelde** olmalı, değilse **404** · `name` zorunlu ≤ 150 ·
`price` 0–100.000 · `validTo >= validFrom` (tek günlük plan geçerlidir) ·
`channel` ∈ `Direct | Phone | WalkIn | BookingCom | Hrs | Expedia | Corporate` veya null.

**Çakışma (409):** aynı `(roomTypeId, channel)` için tarih aralığı kesişen **ikinci aktif** plan
reddedilir — bir gece için iki fiyat geçerli olamaz. Kesişim testi kapalı aralıkla yapılır
(`mevcut.validFrom <= yeni.validTo && yeni.validFrom <= mevcut.validTo`).
Kanal karşılaştırması **birebirdir**: kanala özel plan ile "tüm kanallar" planı (`channel: null`)
çakışma saymaz — belirsizlik yoktur, çünkü fiyat seçiminde kanala özel plan her zaman önce gelir.
Pasif planlar (`isActive: false`) çakışma üretmez.

**Silme:** `RatePlan` soft-delete edilebilir değildir ve `Reservation.RatePlanId` FK'si
`Restrict`'tir; bu yüzden kullanılan bir plan silinemez (**409**) → `isActive: false` ile
pasifleştirilir. Fiyat planı değişikliği **geçmiş rezervasyonların tutarını değiştirmez**
(`Reservation.totalAmount` satış anında dondurulur).

---

## Availability & Occupancy

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/availability` | `Reservations.View` | `?from=&to=&roomTypeId=` — müsait oda listesi + sayılar |
| GET | `/occupancy` | `Reservations.View` | `?from=&to=` — **oda × gün** doluluk matrisi |

> Her iki uç da **aktif otel gerektirir**: matris/liste tek bir otele aittir. Head Office
> kullanıcısı `X-Hotel-Id` göndermezse konsolide moddadır ve hangi otelin takvimi istendiği
> belirsizdir → **400** (`errors: { "X-Hotel-Id": [...] }`).

```jsonc
// GET /availability?from=2026-08-10&to=2026-08-12&roomTypeId=...
{ "from":"2026-08-10", "to":"2026-08-12", "nights":2, "roomTypeId":"guid|null",
  "totalRoomCount":6,          // filtreye uyan tum odalar (servis disi dahil)
  "outOfOrderRoomCount":0,     // musait sayilmazlar
  "availableRoomCount":6,      // aralik boyunca TUM geceleri bos olan odalar
  "byRoomType":[ { "roomTypeId":"guid", "roomTypeCode":"DBL", "availableRoomCount":6 } ],
  "rooms":[ { "roomId":"guid", "roomNumber":"201", "floor":2,
              "roomTypeId":"guid", "roomTypeCode":"DBL", "capacity":2 } ] }
```

> **Müsaitlik yanıtında fiyat alanı YOKTUR.** Tutar yalnızca rezervasyon oluşturulurken
> sunucuda hesaplanır (fiyat mantığı tek yerde kalsın); liste fiyatı gerekiyorsa
> `GET /room-types` (`basePrice`) kullanılır.

```jsonc
// GET /occupancy?from=2026-08-09&to=2026-08-15
{ "from":"2026-08-09", "to":"2026-08-15",
  "days":["2026-08-09","2026-08-10","2026-08-11","2026-08-12","2026-08-13","2026-08-14"],
  "rooms":[
    { "roomId":"guid", "roomNumber":"201", "floor":2,
      "roomTypeId":"guid", "roomTypeCode":"DBL", "isOutOfOrder":false,
      "cells":[
        { "date":"2026-08-10", "reservationId":"guid", "reservationNumber":"RES-2026-00001",
          "guestName":"Jürgen Müller", "status":"Confirmed",
          "isArrival":true, "isDeparture":false },
        { "date":"2026-08-11", "reservationId":"guid", "reservationNumber":"RES-2026-00001",
          "guestName":"Jürgen Müller", "status":"Confirmed",
          "isArrival":false, "isDeparture":true } ] } ],
  "summary":{ "roomCount":12, "days":6, "roomNights":72,
              "occupiedRoomNights":4, "occupancyRate":5.56 } }
```

**Grid sözleşmesi**
- `days` = kolon ekseni; `from` dahil, `to` **hariç** (yarı açık aralık).
- `cells` **seyrektir** (sparse): yalnızca **dolu** geceler için hücre vardır. Boş geceler
  yanıtta yer almaz (yüzlerce `null` taşımamak için); istemci `days` × `cells[].date` ile eşler.
- Bir hücre = **bir oda + bir gece**. `isArrival` ilk gece (`date == checkIn`), `isDeparture`
  **son gece** (`date == checkOut - 1`) demektir: misafir ertesi sabah çıkar, çıkış günü için
  hücre üretilmez — böylece grid çubuğu doğru yerde biter ve ardışık konaklamalar yan yana durur.
- `status` enum adıdır ve grid'de görsel stile karşılık gelir (architecture.md §4.3:
  `Option` = kesikli çizgi vb.).
- `Cancelled`/`NoShow` rezervasyonlar grid'de **görünmez** (odayı bloke etmezler).

**Aralık sınırları (400)**
- `/occupancy`: en fazla **92 gün** (≈3 ay). Yanıt çarpımsal (oda × gün) büyüdüğü için sınır
  düşüktür; istemcinin kazara yıllık matris istemesi sessizce kırpılmaz, **400** döner
  (`errors: { "To": ["Doluluk grid'i araligi en fazla 92 gun olabilir; ..."] }`).
- `/availability`: en fazla **366 gün** (oda başına tek satır döndüğü için daha gevşek).
- İki uçta da `to > from` zorunludur (en az 1 gece).

---

## Reservations

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/reservations` | `Reservations.View` | Sayfalı + filtreli |
| GET | `/reservations/{id}` | `Reservations.View` | Detay çekmecesi |
| POST | `/reservations` | `Reservations.Create` | Rezervasyon sihirbazı; 201 + `Location` |
| PUT | `/reservations/{id}` | `Reservations.Create` | Tarih/oda/kişi/kanal; müsaitlik + tutar yeniden hesap |
| POST | `/reservations/{id}/check-in` | `Reservations.CheckInOut` | `Option`/`Confirmed` → `CheckedIn` |
| POST | `/reservations/{id}/check-out` | `Reservations.CheckInOut` | `CheckedIn` → `CheckedOut`; oda → `Dirty` |
| POST | `/reservations/{id}/cancel` | `Reservations.Create` | `CheckedIn`/`CheckedOut` iptal edilemez → **409** |
| POST | `/reservations/{id}/no-show` | `Reservations.CheckInOut` | `Option`/`Confirmed` → `NoShow` |
| GET | `/reservations/{id}/folio` | `Reservations.View` | Açık hesap satırları + toplamlar |

```jsonc
// ReservationResponse
{ "id":"guid", "reservationNumber":"RES-2026-00001",
  "status":"Confirmed",          // Option | Confirmed | CheckedIn | CheckedOut | Cancelled | NoShow
  "channel":"Direct",            // Direct | Phone | WalkIn | BookingCom | Hrs | Expedia | Corporate
  "roomId":"guid", "roomNumber":"201", "roomTypeId":"guid", "roomTypeCode":"DBL",
  "guestId":"guid", "guestName":"Jürgen Müller", "guestEmail":"juergen.mueller@example.de",
  "checkIn":"2026-08-09", "checkOut":"2026-08-12", "nights":3,   // nights sunucuda hesaplanir
  "adults":2, "children":0,
  "totalAmount":450.00, "currency":"EUR",
  "depositPercent":20.00, "depositAmount":90.00,                 // depositAmount sunucuda hesaplanir
  "ratePlanId":"guid|null", "ratePlanName":"Sommer BAR Doppelzimmer|null",
  "notes":"Drei Nächte", "checkedInAt":null, "checkedOutAt":null,
  "folioId":"guid|null" }

// GET /reservations → PagedResult<ReservationResponse>
// Filtreler: ?page=1&pageSize=20&status=&channel=&roomId=&guestId=&from=&to=&search=
//   from/to -> ARALIKLA KESISEN konaklamalar (from < checkOut && checkIn < to)
//   search  -> rezervasyon numarasi + misafir ad/soyad contains (case-insensitive)
//   siralama: checkIn, sonra oda numarasi (dogal sira), sonra id

// POST /reservations   (totalAmount GONDERILMEZ — gonderilse de yok sayilir)
{ "roomId":"guid", "guestId":"guid",
  "checkIn":"2026-08-10", "checkOut":"2026-08-12",
  "adults":2, "children":0, "channel":"Direct",
  "depositPercent":20, "notes":null,
  "status":"Confirmed" }        // opsiyonel: yalnizca Option | Confirmed (varsayilan Option)

// PUT /reservations/{id}       (status TASINMAZ — durum yalnizca aksiyon uclariyla degisir)
{ "roomId":"guid", "guestId":"guid", "checkIn":"2026-08-09", "checkOut":"2026-08-12",
  "adults":2, "children":0, "channel":"Direct", "depositPercent":20, "notes":null }

// POST /reservations/{id}/cancel   (govde OPSIYONEL)
{ "reason":"Gast hat storniert" }  // verilirse rezervasyon notlarina damgali olarak EKLENIR
```

### `totalAmount` — her zaman sunucuda

İstemciden gelen tutar **yok sayılır** (fiyat manipülasyonu). Hesap:

1. `nights = checkOut - checkIn` (yarı açık aralık).
2. **Her gece için ayrı** fiyat bulunur ve toplanır → sezon geçişinde plan sınırı doğru hesaplanır.
3. Bir gece için fiyat seçim önceliği:
   1. o geceyi kapsayan, **rezervasyonun kanalına özel** aktif plan,
   2. o geceyi kapsayan, **tüm kanallar** için aktif plan (`channel: null`),
   3. plan yoksa **oda tipinin `basePrice`**'ı.
4. Toplam 2 haneye yuvarlanır (`AwayFromZero`).
5. `ratePlanId` = **geliş gecesinin** planı (raporlamada "hangi planla satıldı"); tutar yine
   gece gece hesaplanmış toplamdır. Plan kullanılmadıysa `null`.

`PUT` aynı hesabı yeniden çalıştırır (tarih/oda/kanal değişince eski tutarın kalması yanlış
fiyatlandırma olurdu). Kod: `Application/Features/Reservations/Common/ReservationPricingService.cs`.

### `reservationNumber` üretimi

Biçim **`RES-{yıl}-{5 haneli sıra}`** (örn. `RES-2026-00042`), **otel bazında**:
aynı otelde aynı yılın önekiyle başlayan en büyük numara okunur ve bir artırılır. Sıra sabit
5 hane sıfır dolgulu olduğu için sözlük sırası = sayısal sıra → `ORDER BY ... DESC LIMIT 1`
tek satır okur.

> **GoBD ile karıştırılmamalıdır.** Boşluksuz (kesintisiz) sekans zorunluluğu **faturalar**
> içindir (`HotelInvoiceCounter` + satır kilidi, architecture.md §6.2). Rezervasyon numarası
> ticari bir referanstır: satır kilidi **kullanılmaz** (rezervasyon oluşturma yolu kilitlenmez),
> boşluk oluşabilir, iptal edilen numara yeniden kullanılmaz. Eşzamanlılıkta nihai güvence
> `Reservation(HotelId, ReservationNumber)` **unique index**'idir; çakışma olursa handler
> numarayı yenileyip yeniden dener (en fazla 5 deneme) — kullanıcı hata görmez.

### Durum makinesi (tek yerde)

Tüm geçişler `Application/Features/Reservations/Common/ReservationStatusMachine.cs` içindedir;
handler'lar yalnızca "şu geçişi yapabilir miyim" diye sorar.

| Mevcut durum | İzin verilen hedefler |
|---|---|
| `Option` | `Confirmed`, `CheckedIn`, `Cancelled`, `NoShow` |
| `Confirmed` | `CheckedIn`, `Cancelled`, `NoShow` |
| `CheckedIn` | `CheckedOut` |
| `CheckedOut` / `Cancelled` / `NoShow` | — (nihai) |

Geçersiz geçiş **409** ve mesaj **hangi geçişin denendiğini** söyler:
`"Gecersiz durum gecisi: 'CheckedIn' -> 'Cancelled'. Izin verilen gecisler: CheckedOut."`

`PUT` (içerik değişikliği) yalnızca `Option`, `Confirmed`, `CheckedIn` durumlarında serbesttir
(konaklama uzatma/oda değişikliği meşrudur); nihai durumlarda **409**.

#### `Cancelled` / `NoShow` durumunun faturaya etkisi — Kurtaxe doğmaz

Bu iki durumdaki bir rezervasyondan fatura üretildiğinde **`CityTax` (Kurtaxe) satırı hiç
oluşturulmaz** ve `cityTaxAmount = 0.00` olur; konaklama satırı faturada kalır.

Kurtaxe/Kurbeitrag belediye tüzükleriyle (*Kurbeitragssatzung*) düzenlenir ve vergiyi doğuran olay
(*Steuertatbestand*) **fiilen gerçekleşen konaklamadır** (*Übernachtung*) — vergi kişi ve geçirilen
**gece** başına doğar. Konaklama gerçekleşmediyse vergi de doğmaz; otel belediye adına tahsil edip
aktaracağı tutarı misafirden isteyemez (*durchlaufender Posten*, UStG §10 Abs. 1 Satz 5).
Ayrıntı ve mali onay notları: `docs/api-contracts-invoices.md` → "Kurtaxe ve gerçekleşmeyen
konaklama".

> **Erken çıkış bu fazda kapsam dışıdır:** gece sayısı hâlâ `checkIn`/`checkOut` aralığından gelir.
> `checkedInAt`/`checkedOutAt` UTC **an** damgasıdır ve otelin saat dilimi modellenmediği için
> fiilî gece sayısı gün sınırında kayabilir — şema ihtiyacı olarak açık bırakıldı.

#### Rezervasyonun oda silmeye etkisi (GoBD / AO §147)

`DELETE /api/v1/rooms/{id}` iki bağımsız nedenle **409** döner:

| # | Koşul | Mesaj |
|---|---|---|
| 1 | Odanın **gelecek tarihli** (`checkOut >= bugün`), iptal edilmemiş rezervasyonu var | "gelecek tarihli rezervasyonu var …" |
| 2 | Odanın **iptal edilmemiş** ve **henüz faturalanmamış** rezervasyonu var (tarihi geçmiş olsa bile) | "… henüz faturalanmamış bir rezervasyonu var (**RES-…**, tarih aralığı) …" |

- **"Faturalanmış" = yürürlükteki belge:** fatura *iptal edilmemiş*, *kendisi Stornorechnung değil*
  ve *numara almış* (`issuedAt != null`). Yani **taslak fatura saymaz** — taslağı olan bir
  rezervasyonun odası da silinemez.
- **Neden:** oda soft-delete edilince rezervasyonun zorunlu oda navigasyonu global query filter'a
  takılır; rezervasyon liste ve detaydan **404** olur ve bir daha **faturalanamaz** — tutarı
  raporlarda `unbilledRoomRevenueGross` altında asılı kalır. GoBD ve **AO §147** ticari kayıtların
  10 yıl boyunca *erişilebilir* ve *makine ile değerlendirilebilir* kalmasını ister.
- **Engel kaldırılabilir:** rezervasyon faturalanır (veya iptal edilir), sonra oda silinir.
  İptal edilmiş rezervasyon her iki kuralı da tetiklemez.

### Check-in / check-out kuralları

- **Check-in (409 halleri):** giriş tarihinden **önce** (`bugün < checkIn`); oda `isOutOfOrder`;
  durum `Option`/`Confirmed` değil. Geç check-in serbesttir (misafir bir gün sonra gelebilir).
- **Check-out:** yalnızca `CheckedIn`. Odanın `housekeepingStatus`'u **otomatik `Dirty`** olur
  (architecture.md §5) ve bu değişiklik rezervasyonla **aynı `SaveChanges`** içinde yazılır →
  "çıkış yapıldı ama oda temiz görünüyor" ara durumu oluşamaz. Servis dışı oda `Dirty`'ye
  çekilmez (`isOutOfOrder ↔ OutOfOrder` değişmezi korunur).
- **Folio check-out'ta kapatılmaz:** fatura henüz yok, açık hesap durur (kapatma faturalama
  modülünün işi).
- `checkedInAt` / `checkedOutAt` UTC zaman damgalarıdır.

### Folio (açık hesap)

Rezervasyon oluşturulurken folio **aynı işlemde** açılır ve tek bir `RoomCharge` satırı yazılır;
`PUT` sonrasında bu satır yeni tutara göre güncellenir. Ekstra harcamalar ve `CityTax` (Kurtaxe)
satırları faturalama modülüyle gelecektir.

```jsonc
// GET /reservations/{id}/folio
{ "reservationId":"guid", "reservationNumber":"RES-2026-00001",
  "folioId":"guid|null", "isClosed":false, "currency":"EUR", "guestName":"Jürgen Müller",
  "lines":[ { "id":"guid", "type":"RoomCharge",       // RoomCharge | Extra | CityTax
              "description":"Ubernachtung 2026-08-09 - 2026-08-12",
              "quantity":3.00, "unitPrice":150.00,    // unitPrice GOSTERIM icin (gece ortalamasi)
              "vatRate":7.00,                          // otelin INDIRIMLI orani (konaklama)
              "lineNet":420.56, "lineVat":29.44, "lineGross":450.00,
              "serviceDate":"2026-08-09" } ],          // Leistungsdatum (GoBD)
  "totalNet":420.56, "totalVat":29.44, "totalGross":450.00 }
```

- KDV oranı **otelin vergi profilinden** (`TaxProfile.reducedVatRate`, DE: %7) alınır ve satıra
  **kopyalanır**: oran sonradan değişse bile mevcut belge değişmez (koda hardcode edilmez).
- Fiyatlar **brüt**tür (misafirin gördüğü tutar); net/KDV brütten ayrıştırılır ve
  `lineNet + lineVat = lineGross` her zaman tutar.
- Folio henüz açılmamışsa `folioId: null`, `lines: []`, toplamlar `0` döner (istemci ayrı bir
  "folio yok" durumu ele almaz).

### Doğrulama kuralları (400 + `errors`)

- `roomId`, `guestId` zorunlu; **aynı otelde** olmalı, değilse **404** (varlık sızdırılmaz)
- `checkOut > checkIn` (en az 1 gece; aynı gün çıkış = day-use bu fazda desteklenmez)
- konaklama en fazla **365 gece**
- `adults` 1–20, `children` 0–20; **`adults + children` oda tipinin `capacity`'sini aşamaz**
  (`errors: { "Adults": ["'201' numarali odanin kapasitesi 2 kisi; 4 kisi secildi."] }`)
- `channel` enum adı; `depositPercent` 0–100; `notes` ≤ 1000; `cancel.reason` ≤ 500
- `POST` gövdesindeki `status` yalnızca `Option` veya `Confirmed` olabilir

### 409 (Conflict) halleri — özet

| Durum | Mesaj örneği |
|---|---|
| Tarih çakışması | `'201' numarali oda 2026-08-11 - 2026-08-13 araliginda musait degil: 'RES-2026-00001' rezervasyonu (2026-08-10 - 2026-08-12) ile cakisiyor.` |
| Servis dışı oda | `'204' numarali oda servis disi (out of order); rezervasyon alinamaz.` |
| Erken check-in | `Check-in giris tarihinden once yapilamaz. Giris tarihi: 2026-08-10, bugun: 2026-07-30.` |
| Geçersiz geçiş | `Gecersiz durum gecisi: 'CheckedIn' -> 'Cancelled'. Izin verilen gecisler: CheckedOut.` |
| Misafir silme | `Bu misafirin aktif veya gelecek tarihli rezervasyonu var; ...` |
| Fiyat planı çakışması | `Bu oda tipi ve kanal (tum kanallar) icin tarih araligi cakisan bir fiyat plani var: ...` |
| Kullanılan plan silme | `Bu fiyat plani rezervasyonlarda kullanildigi icin silinemez; plani pasife alin (isActive = false).` |

---

## Şema notu (yeni migration gerekmedi)

Rezervasyon modülü **mevcut şemayla** uygulandı: `Guest`, `RatePlan`, `Reservation`, `Folio`,
`InvoiceLineItem` entity'leri ve `Reservation(HotelId, ReservationNumber)` (filtreli unique) +
`Reservation(HotelId, RoomId, CheckIn, CheckOut)` index'leri ilk migration'da mevcuttu.
**Domain/Infrastructure'da hiçbir değişiklik yapılmadı.**
