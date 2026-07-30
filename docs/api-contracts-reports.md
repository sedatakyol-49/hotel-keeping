# HotelCore — API Sözleşmesi: Raporlama Modülü

> Bu dosya **`docs/api-contracts.md`'ye taşınmak üzere** hazırlanmış raporlama modülü bölümüdür
> (Main Agent birleştirir; ana dosyadaki "Reports — henüz uygulanmadı" taslağının yerine geçer).
> Biçim, ana dosyadaki "Personel" bölümünü taklit eder. **Kaynak-of-truth** yine backend'in
> ürettiği OpenAPI şemasıdır (`/swagger/v1/swagger.json`). Genel kurallar (base URL,
> `Authorization`, `X-Hotel-Id`, `Accept-Language`, `ProblemDetails`) ana dosyada geçerlidir ve
> burada tekrarlanmaz.

Kapsanan uçlar: **Occupancy Report** (doluluk) ve **Revenue Report** (ciro / ADR / RevPAR /
kanal dağılımı).

---

## Uçlar

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/reports/occupancy?from=&to=` | `Reports.View` | Oda-gece, kapasite, doluluk + günlük seri |
| GET | `/reports/revenue?from=&to=` | `Reports.View` | Ciro, ADR, RevPAR, kanal dağılımı + günlük seri |

> **RBAC:** `Reports.View` yalnızca `Admin`, `HeadOfficeManager`, `HotelManager` ve `Accountant`
> rollerindedir (architecture.md §7). Resepsiyon ve housekeeping ciro **görmez**; kural
> backend'de policy ile uygulanır, rol adı controller'a hardcode edilmez.

> **Aktif otel ZORUNLU DEĞİLDİR.** Head Office kullanıcısı `X-Hotel-Id` göndermezse rapor
> **konsolide** hesaplanır. Bu, `/availability` ve `/occupancy` (grid) uçlarından **bilinçli bir
> farktır**: bir takvim matrisi tek bir otele aittir, ama konsolide KPI Head Office'in var olma
> sebebidir. Bkz. "Kapsam (scope) ve konsolide mod".

---

## Temel karar: dönem **kapalı** gün aralığıdır `[from, to]`

Rezervasyon/müsaitlik modülü **yarı açık** `[checkIn, checkOut)` kullanır çünkü orada birim
**gece**dir. Rapor ise bir **gün kümesi** üzerinde konuşur ("1–7 Eylül ciro"), bu yüzden:

- `from` ve `to` **her iki uç da dâhildir**; `to == from` **tek günlük** rapordur (geçerli).
- `dayCount = to − from + 1`.
- Sayılan geceler, bu günlerde **başlayan** gecelerdir. Yani gece penceresi yarı açık
  `[from, to + 1 gün)`'dür ve rezervasyon modülünün kararına **birebir uyar**:
  **çıkış günü gece saymaz.**
- Bir konaklama aralığa taşarsa (`checkIn < from` veya `checkOut > to+1`) **yalnızca pencereye
  düşen geceleri** sayılır (kırpma).

> Aralık en fazla **366 gün** olabilir (`to − from + 1 <= 366`). Doluluk *grid*'indeki 92 gün
> sınırı yanıtın `oda × gün` **matrisi** olmasındandı; rapor yanıtı toplamlar + gün başına tek
> satır döndürür, yani **doğrusal** büyür — bu yüzden sınır müsaitlik ucuyla aynı yere, bir yıla
> çekilmiştir. Aşılırsa **400** (sessizce kırpılmaz).

---

## Metrik tanımları

| Metrik | Tanım |
|---|---|
| `soldRoomNights` | **Satılan oda-gece.** Odayı bloke eden bir rezervasyonun rapor penceresiyle kesişen gece sayısı. Her rezervasyon tek bir odaya bağlı olduğu için "gece" = "oda-gece". |
| `physicalRoomNights` | Oda sayısı × gün sayısı (servis dışı odalar **dâhil**). |
| `outOfOrderRoomNights` | Servis dışı (`isOutOfOrder`) oda sayısı × gün sayısı. |
| `availableRoomNights` | `physical − outOfOrder` → **satılabilir** kapasite. Doluluk ve RevPAR'ın paydası. |
| `occupancyRate` | `sold / available × 100` (2 ondalık). |
| `adrNet` / `adrGross` | **ADR** = oda geliri / `soldRoomNights`. Yalnızca konaklama geliri; ekstra ve Kurtaxe **girmez**. |
| `revParNet` / `revParGross` | **RevPAR** = oda geliri / `availableRoomNights` (= ADR × doluluk). |

**Bloke eden rezervasyon:** durumu `Cancelled` **ve** `NoShow` olmayan rezervasyon — kural
müsaitlik modülüyle **tek yerdedir** (`AvailabilityQuery.IsBlocking`). Yani doluluk raporu ile
oda takvimi hiçbir zaman çelişmez. `NoShow` satılmış sayılmaz (misafir konaklamamıştır);
gelmeyene kesilen iptal bedeli ADR'yi bozmasın diye ayrı alanda raporlanır
(`otherInvoicedRevenue`).

**Servis dışı odalar müsait kapasiteden DÜŞÜLÜR.** Gerekçe: tadilattaki/arızalı oda satılabilir
envanter değildir; kapasiteye dâhil edilirse doluluk yapay olarak düşer ve müdürün elinde olmayan
bir sebep performans gibi görünür (otelcilik pratiğinde OOO odalar envanterden çıkarılır).
**Üç sayı da yanıtta ayrı ayrı döner** (`physicalRoomNights`, `outOfOrderRoomNights`,
`availableRoomNights`) — tüketici isterse fiziksel kapasiteye göre kendi doluluk tanımını kurar.

> **`occupancyRate` %100'ü aşabilir** ve **kırpılmaz**: `Room.IsOutOfOrder` tarihsiz bir *anlık
> durum* bayrağıdır, bugün servis dışına alınan bir oda geçmişte dolu olmuş olabilir. Gerçek
> gizlenmez (bkz. "Bilinen sınırlar").

**Payda sıfırsa metrik 0 döner** (`null` değil, hata değil): "hiç oda yok" / "hiç gece satılmadı"
durumunda ADR/RevPAR/doluluk tanımsızdır; alanın tipi her zaman sayı kalır.

**`RevPAR = ADR × doluluk` özdeşliği tanım gereği sağlanır** (üç metrik de aynı `sold` ve
`available` sayılarını kullanır). Sunucu yine de RevPAR'ı **iki yoldan** hesaplayıp karşılaştırır;
0,01'den büyük sapmada uyarı loglar (yanıt değiştirilmez — sessizce "düzeltilmiş" sayı dönmez).

---

## Gelirin kaynağı: **kesinleşmiş faturalar**

İki aday vardı — **faturalar** (muhasebe gerçeği) ve **`Reservation.TotalAmount`** (operasyonel
görünüm). Ciro raporu muhasebeyle **tutarlı olmak zorundadır**; "ciro" kelimesi iki farklı sayıyı
gösteremez. Bu yüzden birincil kaynak **faturalardır**.

> ⚠️ **Henüz faturalanmamış konaklamalar bu ciroya GİRMEZ.** Fark gizlenmez: rezervasyon tabanlı
> görünüm `unbilledRoomRevenueGross` alanında **ayrı** döner (bkz. aşağıda). Tek bir sayı iki
> anlamda kullanılmaz.

### Hangi faturalar sayılır

Filtre **`Invoice.IssuedAt != null`** — yani **bir kez numara almış** (kesinleşmiş) her belge.

| Fatura hâli | Sayılır mı | Gerekçe |
|---|---|---|
| `Draft` | **Hayır** | Taslak belge değildir, numarası yoktur, terk edilebilir. |
| Taslakken iptal edilen (`Cancelled`, `issuedAt = null`) | **Hayır** | Hiç belge olmadı. `status` yerine `issuedAt`'e bakılmasının sebebi tam olarak bu iki `Cancelled` hâlini ayırmaktır. |
| `Finalized` / `Paid` | **Evet** | Ödeme durumu ciroyu **etkilemez**: ciro tahakkuk esaslıdır (Soll-Versteuerung), tahsilat değildir. |
| Kesinleştikten sonra iptal edilen (`Cancelled`, `issuedAt` dolu) | **Evet** | ↓ |
| **Stornorechnung** (negatif ayna belge) | **Evet** | ↑ İkisi birlikte sayıldığı için **tam sıfır** ederler. Yalnızca storno sayılsaydı rapor **hayali negatif ciro** gösterirdi; netleştirme ancak çiftin iki tarafı da sayıldığında doğrudur. |

### Döneme atıf: konaklama gecelerine **eşit dağıtım** (Periodenabgrenzung)

Rezervasyona bağlı bir faturanın `RoomCharge` satırı konaklamanın **tamamı** için tek satırdır
(`quantity = gece sayısı`, `serviceDate = giriş günü`). Belge tarihine (`issuedAt`) veya
Leistungsdatum'a göre atıf yapılsaydı 5 gecelik bir konaklamanın **tüm** geliri giriş gününe
düşer, dönem sınırındaki konaklamalar tümüyle bir tarafa yazılır ve `ADR = gelir / oda-gece`
anlamını yitirirdi (pay ile payda farklı gecelere ait olurdu).

Bu yüzden rezervasyona bağlı gelir **konaklamanın gecelerine eşit dağıtılır**:

```
dönem geliri = konaklamanın toplam geliri × (penceredeki gece) / (toplam gece)
```

Böylece gelir ve oda-gece **aynı gecelere** aittir; ADR/RevPAR anlamlıdır ve günlük seri
çizilebilir. Aynı kural ekstralara ve Kurtaxe'ye de uygulanır (tek bir atıf tabanı).

### Dağıtılamayan gelir ayrı bloktadır

Rezervasyona bağlı **olmayan** faturalar (elle kesilen) ve `Cancelled`/`NoShow` rezervasyona bağlı
faturalar (iptal bedeli / Ausfallentschädigung) bir konaklama gecesine dağıtılamaz. Bunlar
`otherInvoicedRevenue` bloğunda, satırın **Leistungsdatum**'una (`InvoiceLineItem.serviceDate`)
göre raporlanır ve:

- `totalRevenue`'ya **dâhil DEĞİLDİR**,
- ADR/RevPAR'a **girmez** (bir oda-gecesi karşılığı yoktur).

Toplam muhasebe cirosu isteniyorsa: `totalRevenue.net + otherInvoicedRevenue.total.net`.
`serviceDate`'i boş olan satır **sayılmaz** (hizmet tarihi GoBD zorunlu alanıdır ve sunucu her
zaman doldurur; boş satırı keyfî bir güne yazmak yerine dışarıda bırakmak dürüst davranıştır).

### Kurtaxe **gelir değildir**

Belediyenin misafirden aldığı, otelin yalnızca tahsil edip aktardığı kalemdir (durchlaufender
Posten — fatura modülüyle aynı gerekçe). `cityTaxCollected` **ayrı** alandır; ne `totalRevenue`'ya
ne ADR'ye girer.

### Net mi brüt mü

**İkisi de döner ve alan adı açıktır**: her para bloğu `{ net, vat, gross }` şeklindedir ve
`net + vat == gross` her zaman tutar. Birincil ciro **net**tir (KDV devlete aittir, otelin geliri
değildir). ADR ve RevPAR'ın **hem net hem brüt** sürümü ayrı alanlardadır (`adrNet`/`adrGross`,
`revParNet`/`revParGross`) — tek bir sayı iki anlamda kullanılmasın diye.

**Yuvarlama:** 2 ondalık, kaufmännisch (yarım yukarı) — fatura modülüyle aynı. Ara toplamlar tam
`decimal` hassasiyetinde biriktirilir, yuvarlama **yalnızca yanıt üretilirken** yapılır. Bunun
görünür sonucu: **günlük serinin yuvarlanmış değerlerinin toplamı üst seviye toplamdan birkaç
kuruş sapabilir**; üst seviye toplamlar esastır.

---

## Kapsam (scope) ve konsolide mod

Her iki yanıt da bir `scope` nesnesi ve bir `byHotel` kırılımı içerir.

```jsonc
"scope": { "mode": "Hotel",              // Hotel | Consolidated
           "hotelId": "guid|null",       // konsolide modda null
           "hotelCount": 1,
           "currency": "EUR",            // ortak para birimi; karisiksa null
           "hasMixedCurrencies": false }
```

- **`Hotel`**: `X-Hotel-Id` gönderildi (veya kullanıcının varsayılan oteli var). Rapor tek oteldir.
- **`Consolidated`**: Head Office kullanıcısı aktif otel seçmedi. Rapor **erişilebilir tüm
  otelleri** kapsar. Portföy doluluğu (`Σsatılan / Σmüsait`) otelcilikte standart bir büyüklüktür,
  bu yüzden uç hata vermez.
- **`byHotel` her zaman döner** (tek otel modunda tek eleman): konsolide bir sayı her zaman otel
  bazına ayrıştırılabilir olmalıdır — konsolide ADR farklı segment/para birimlerini karıştırabilir.
- **`hasMixedCurrencies: true`** ise kapsamdaki oteller farklı para birimleri kullanıyordur;
  üst seviye para toplamları farklı birimlerin aritmetik toplamıdır ve **kullanılmamalıdır** —
  `byHotel` esas alınır. Sayı gizlenmez, **etiketlenir**.

> **Tenant izolasyonu:** kapsam `UserHotelAccess` üzerinden (`allHotels` yetkisinde: kullanıcının
> Head Office'ine bağlı oteller) çözülür ve **tüm** sorgular bu otel kimlikleriyle ek olarak
> daraltılır. Global query filter hiçbir yerde atlanmaz; bu bir **daraltmadır**, bypass değil.

---

## Şekiller

### `GET /reports/occupancy?from=2026-09-01&to=2026-09-07`

```jsonc
{
  "from": "2026-09-01",
  "to": "2026-09-07",              // DAHIL
  "dayCount": 7,
  "scope": { "mode":"Hotel", "hotelId":"guid", "hotelCount":1,
             "currency":"EUR", "hasMixedCurrencies":false },

  "roomCount": 12,
  "outOfOrderRoomCount": 2,
  "physicalRoomNights": 84,        // 12 x 7
  "outOfOrderRoomNights": 14,      //  2 x 7
  "availableRoomNights": 70,       // 10 x 7  <- doluluk paydasi
  "soldRoomNights": 14,
  "occupancyRate": 20.00,

  "daily": [                       // dayCount kadar eleman (grafik ekseni)
    { "date":"2026-09-01", "soldRoomNights":2, "availableRoomNights":10, "occupancyRate":20.00 }
    // ...
  ],

  "byHotel": [
    { "hotelId":"guid", "hotelName":"HotelCore Berlin Mitte",
      "roomCount":12, "outOfOrderRoomCount":2,
      "physicalRoomNights":84, "outOfOrderRoomNights":14,
      "availableRoomNights":70, "soldRoomNights":14, "occupancyRate":20.00 }
  ]
}
```

> Doluluk raporunda **para alanı yoktur**; kanal dağılımı ve ADR ciro raporundadır.

### `GET /reports/revenue?from=2026-09-01&to=2026-09-07`

```jsonc
{
  "from": "2026-09-01", "to": "2026-09-07", "dayCount": 7,
  "scope": { "mode":"Hotel", "hotelId":"guid", "hotelCount":1,
             "currency":"EUR", "hasMixedCurrencies":false },

  // Doluluk raporuyla AYNI tanimlar (ADR/RevPAR'in payda kaynagi)
  "soldRoomNights": 14,
  "availableRoomNights": 70,
  "outOfOrderRoomNights": 14,
  "physicalRoomNights": 84,
  "occupancyRate": 20.00,

  "roomRevenue":  { "net":2112.14, "vat":147.86, "gross":2260.00 },   // InvoiceLineType.RoomCharge
  "extraRevenue": { "net":0.00,    "vat":0.00,   "gross":0.00 },      // InvoiceLineType.Extra
  "totalRevenue": { "net":2112.14, "vat":147.86, "gross":2260.00 },   // room + extra (Kurtaxe HARIC)

  "cityTaxCollected": 39.00,        // Kurtaxe — GELIR DEGILDIR, ADR'ye girmez

  "adrNet": 150.87,   "adrGross": 161.43,       // oda geliri / soldRoomNights
  "revParNet": 30.17, "revParGross": 32.29,     // oda geliri / availableRoomNights

  // Operasyonel karsilastirma: kesinlesmis faturasi OLMAYAN konaklamalarin
  // Reservation.totalAmount tutarindan gecelere dusen pay. CIRO DEGILDIR, hicbir toplama girmez.
  "unbilledRoomRevenueGross": 258.00,

  // Konaklama gecelerine DAGITILAMAYAN kesinlesmis fatura geliri (elle kesilen faturalar +
  // iptal/no-show rezervasyona bagli faturalar), Leistungsdatum'a gore donemlenmis.
  // totalRevenue'ya DAHIL DEGILDIR.
  "otherInvoicedRevenue": {
    "room":  { "net":0.00,  "vat":0.00,  "gross":0.00 },
    "extra": { "net":84.03, "vat":15.97, "gross":100.00 },
    "total": { "net":84.03, "vat":15.97, "gross":100.00 },
    "cityTaxCollected": 0.00
  },

  "byChannel": [                    // net oda gelirine gore AZALAN sirada
    { "channel":"Direct",           // ReservationChannel enum ADI
      "reservationCount":3,         // donemle KESISEN rezervasyon sayisi (gece basina degil)
      "soldRoomNights":7,
      "roomRevenue":  { "net":1056.07, "vat":73.93, "gross":1130.00 },
      "extraRevenue": { "net":0.00, "vat":0.00, "gross":0.00 },
      "cityTaxCollected":24.00,
      "adrNet":150.87,
      "roomRevenueShare":50.00 }    // toplam NET oda geliri icindeki pay, yuzde
  ],

  "byHotel": [
    { "hotelId":"guid", "hotelName":"HotelCore Berlin Mitte", "currency":"EUR",
      "soldRoomNights":14, "availableRoomNights":70, "occupancyRate":20.00,
      "roomRevenue":{...}, "extraRevenue":{...}, "totalRevenue":{...},
      "cityTaxCollected":39.00, "adrNet":150.87, "revParNet":30.17 }
  ],

  "daily": [                        // dayCount kadar eleman
    { "date":"2026-09-01", "soldRoomNights":2, "availableRoomNights":10, "occupancyRate":20.00,
      "roomRevenue":{ "net":407.48, "vat":28.53, "gross":436.01 },
      "extraRevenue":{ "net":0.00, "vat":0.00, "gross":0.00 },
      "cityTaxCollected":9.00, "adrNet":203.74, "revParNet":40.75 }
  ]
}
```

---

## Doğrulama kuralları (400 + `errors`)

| Kural | Hata anahtarı / mesaj |
|---|---|
| `to >= from` (eşit olabilir → tek günlük rapor) | `To`: `'to' tarihi 'from' tarihinden once olamaz (tek gunluk rapor icin esit olabilir).` |
| `to − from + 1 <= 366` | `To`: `Rapor araligi en fazla 366 gun olabilir; daha uzun donemler icin araligi bolerek sorgulayin.` |
| `from` / `to` zorunlu | `From` / `To` |

| Durum | Kod |
|---|---|
| Doğrulama hatası (aralık) | 400 |
| Token yok/geçersiz | 401 |
| `Reports.View` izni yok | 403 |
| Bozuk `X-Hotel-Id` GUID / erişilemeyen otel | 400 / 403 (middleware, endpoint hiç çalışmaz) |

> **404 yoktur:** rapor bir koleksiyon değil bir hesaptır; veri yoksa sıfırlarla dolu geçerli bir
> yanıt döner (grafik "veri yok" hâlini ayrıca ele almak zorunda kalmaz).

---

## Performans

- Tüm toplamlar **SQL'de** hesaplanır (`GROUP BY` + `SUM`/`COUNT`); satırlar tek tek belleğe
  **çekilmez**.
- **Sorgu sayısı sabittir ve aralıktan bağımsızdır:**
  `/reports/occupancy` → **3**, `/reports/revenue` → **6** SQL komutu (7 günlük ve 366 günlük
  istek için aynı; PostgreSQL sunucu log'uyla doğrulanmıştır). Döngü içinde sorgu **yoktur**.
- **Günlük seri nasıl üretiliyor:** gün listesi **bellekte** üretilir (en fazla 366 eleman);
  SQL'den `(otel, giriş, çıkış, kanal)` bazında toplanmış **kova**lar gelir ve bu kovalar gün
  eksenine yayılır. Kova anahtarı giriş/çıkış içerdiği için hangi günlere yayılacağı bellekte
  bilinir; yayma maliyeti toplam oda-gece ile sınırlıdır. Kova sayısı hiçbir zaman rezervasyon
  sayısını aşamaz, tipik olarak çok daha azdır. Yani: **toplamlar SQL'de, gün ekseni bellekte.**

---

## Bilinen sınırlar (ürün kararı gerektirir)

1. **`Room.IsOutOfOrder` tarih aralığı taşımaz** — anlık durum bayrağıdır. Servis dışılık tüm
   rapor dönemine uygulanır, yani *geçmiş* raporlar bugünkü servis dışı odalara göre hesaplanır.
   Tarihsel doğruluk için tarih aralıklı bir `RoomBlock` kaydı gerekir (**şema değişikliği**).
2. **Odalar soft-delete edilir**; bugün silinen bir oda geçmiş kapasiteden de düşer.
3. **Konaklama geliri gecelere eşit dağıtılır.** Gerçek gecelik fiyat sezon içinde değişebilir
   (rezervasyon modülü gece gece fiyatlar) ama faturaya tek satır olarak yazılır. Gece bazında
   kesin atıf istenirse fatura satırlarının **gece gece** üretilmesi gerekir.
4. **Ekstralar konaklamaya bağlıysa gecelere dağıtılır**, kendi `serviceDate`'lerine göre değil —
   tek bir atıf tabanı korunsun diye. Ekstralar ADR'ye girmediği için etkisi sınırlıdır.
5. **`occupancyRate` %100'ü aşabilir** (bkz. 1). Kırpılmaz.
6. **Kanal dağılımı rezervasyonun kanalına göredir**; komisyon/net-net gelir hesabı bu fazda
   yoktur (OTA komisyonu modelde tutulmuyor).
