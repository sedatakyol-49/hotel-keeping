# HotelCore — API Sözleşmesi: Faturalama (Rechnung, GoBD)

> **Bu dosya geçicidir.** İçeriği `docs/api-contracts.md` içindeki **"Invoices (GoBD)"**
> bölümünün yerine geçmek üzere yazılmıştır (Main Agent taşıyacak). Biçim, ana dosyadaki
> **"Personel"** bölümüyle aynıdır. Genel kurallar (base URL, auth, `X-Hotel-Id`,
> `Accept-Language`, sayfalama, `ProblemDetails`) ana dosyada geçerlidir ve burada tekrarlanmaz.

### Invoices (GoBD) — **uygulandı**

| Method | Path | İzin | Not |
|---|---|---|---|
| GET | `/invoices` | `Invoices.View` | Sayfalı + filtreli |
| GET | `/invoices/{id}` | `Invoices.View` | Satırlar + ödemeler + **denetim izi** |
| POST | `/invoices` | `Invoices.Create` | **Draft** oluşturur (numara YOK), 201 + `Location` |
| PUT | `/invoices/{id}` | `Invoices.Create` | **Yalnızca Draft**; Finalized/Paid/Cancelled → **409**. İze `Updated` yazılır |
| POST | `/invoices/{id}/finalize` | `Invoices.Approve` | Numara atanır, `issuedAt` damgalanır → `Finalized` |
| POST | `/invoices/{id}/cancel` | `Invoices.Cancel` | Draft → doğrudan iptal; kesinleşmiş → **Stornorechnung** |
| POST | `/invoices/{id}/payments` | `Invoices.Create` | Ödeme kaydı (`PaymentRecorded`); toplam brüte ulaşınca `Paid` |
| GET | `/invoices/{id}/pdf` | `Invoices.View` | **501 Not Implemented** (bu fazda üretilmiyor) |

> **DELETE uç noktası bilinçli olarak YOKTUR** (architecture.md §6.1/§6.4): fatura silinmez.
> Kesinleşmiş fatura hiçbir yoldan değiştirilemez; düzeltme yalnızca iptal faturasıyla yapılır.
> Faturalar soft-delete edilebilir bir entity olsa da (10 yıl saklama), API bunu açmaz.

#### Durum makinesi

```
Draft ──finalize──► Finalized ──ödeme tamamlanınca──► Paid
  │                    │                                │
  └──cancel───────────►└──cancel (Stornorechnung)◄───────┘
     (storno YOK)         (orijinal korunur)
```

- **Draft**: numarasız, serbestçe düzenlenebilir, belge değildir.
- **Finalized**: numaralı, `issuedAt` damgalı, **değiştirilemez** (GoBD §6.1).
- **Paid**: toplam ödeme brüt tutara ulaştı.
- **Cancelled**: taslakta doğrudan; kesinleşmişte yalnızca bir Stornorechnung ile.

#### Şekiller

```jsonc
// InvoiceResponse  (GET /invoices → PagedResult<InvoiceResponse>)
{ "id":"guid",
  "invoiceNumber":"2026-000001",     // TASLAKTA null — numara finalize'da atanır
  "status":"Finalized",              // enum ADI: Draft | Finalized | Paid | Cancelled
  "issuedAt":"2026-07-30T12:35:36.481809+00:00",   // taslakta null
  "guestId":"guid", "guestName":"Anna Mueller",
  "reservationId":"guid|null", "reservationNumber":"RES-0001|null",
  "culture":"de", "currency":"EUR",
  "netAmount":399.49,                // KDV'li satırların net toplamı (Kurtaxe HARİÇ)
  "vatAmount":32.51,
  "cityTaxAmount":12.00,             // Kurtaxe — KDV matrahına dâhil DEĞİL
  "grossAmount":444.00,              // net + KDV + Kurtaxe
  "paidAmount":0.00, "outstandingAmount":444.00,
  "cancelledByInvoiceId":"guid|null",   // bu faturayı iptal eden Stornorechnung
  "cancelsInvoiceId":"guid|null",       // bu fatura bir storno ise iptal ettiği fatura
  "isCancellationInvoice":false,
  "createdAt":"2026-07-30T12:33:33.7+00:00" }

// InvoiceDetailResponse  (GET /invoices/{id}) — yukarıdaki tüm alanlar + üç koleksiyon
{ "...": "InvoiceResponse alanlarının tamamı",
  "lineItems":[
    { "id":"guid", "type":"RoomCharge",        // RoomCharge | Extra | CityTax
      "description":"Room charge 2026-08-01 - 2026-08-04 (3 x night, room 201/DBL)",
      "quantity":3.00,
      "unitPrice":129.00,                     // BRÜT birim fiyat (KDV dâhil)
      "vatRate":7.00,                         // sunucu belirler, istemci GÖNDEREMEZ
      "lineNet":361.68, "lineVat":25.32, "lineGross":387.00,
      "serviceDate":"2026-08-01",             // Leistungsdatum (GoBD)
      "sortOrder":0 },
    { "id":"guid", "type":"CityTax",
      // Muafiyet açıkken açıklama muafiyeti ve (biliniyorsa) yaş sınırını belirtir:
      "description":"City tax (Kurtaxe) 2 person(s) x 2 night(s) - children under 18 exempt",
      "quantity":4.00,                        // vergiye tabi kişi × gece
      "unitPrice":3.00, "vatRate":0.00,
      "lineNet":12.00, "lineVat":0.00, "lineGross":12.00,
      "serviceDate":"2026-08-01", "sortOrder":2 } ],
  "payments":[
    { "id":"guid", "method":"Card",           // Cash | Card | Transfer
      "amount":100.00, "paidAt":"2026-07-30T12:36:00+00:00", "reference":"TERM-4711" } ],
  "auditTrail":[                              // append-only, en eskiden yeniye (GoBD §6.3)
    { "id":"guid", "action":"Created",
      // Created | Updated | Finalized | PaymentRecorded | Paid | Cancelled
      "performedByUserId":"guid|null",
      "performedAt":"2026-07-30T12:33:33.781674+00:00",
      "details":"{\"source\":\"reservation\",\"grossAmount\":444.00, ...}" } ] }
```

#### Yazma gövdeleri

```jsonc
// POST /invoices — YOL A: rezervasyondan (satırlar sunucuda üretilir)
{ "reservationId":"guid", "culture":"de" }     // lineItems GÖNDERİLMEZ (gönderilirse 400)

// POST /invoices — YOL B: elle
{ "guestId":"guid",                            // A yolunda rezervasyondan gelir, B'de ZORUNLU
  "culture":"de",                              // opsiyonel: istek → misafir → otel varsayılanı
  "lineItems":[
    { "type":"RoomCharge",                     // RoomCharge | Extra | CityTax
      "description":"Übernachtung Doppelzimmer",
      "quantity":2, "unitPrice":110.00,        // unitPrice = BRÜT (KDV dâhil)
      "serviceDate":"2026-07-20" } ] }         // opsiyonel; yoksa fatura günü

// PUT /invoices/{id} — yalnızca Draft; satırlar TAMAMEN değiştirilir
{ "guestId":"guid|null",       // null/eksik → değişmez. Rezervasyona bağlı faturada değiştirilemez (409)
  "culture":"en|null",         // null/eksik → değişmez
  "lineItems":[ /* en az 1 satır */ ] }

// POST /invoices/{id}/cancel  (gövde OPSİYONEL)
{ "reason":"Misafir talebi: yanlış oda tipi faturalandı" }   // ≤ 500, denetim izine yazılır
// → 200 + **ORİJİNAL** faturanin InvoiceDetailResponse'u (storno DEĞİL).
//   Kesinlesmis faturada: status=Cancelled ve cancelledByInvoiceId dolu → storno'ya bu alandan
//   ulasilir (GET /invoices/{cancelledByInvoiceId}). Taslakta storno uretilmez, alan null kalir.

// POST /invoices/{id}/payments
{ "method":"Card", "amount":100.00,
  "paidAt":"2026-07-30T12:36:00Z",   // opsiyonel; yoksa sunucu saati. Gelecek tarih → 400
  "reference":"TERM-4711" }          // opsiyonel ≤ 128
// → 200 + InvoiceDetailResponse (ödeme ayrı adreslenebilir kaynak değil, bu yüzden 201 değil)
```

#### Liste filtreleri

```
GET /invoices?page=1&pageSize=20&status=&guestId=&reservationId=&from=&to=&search=
```
- `status` ∈ `Draft | Finalized | Paid | Cancelled`
- `from` / `to`: **`issuedAt`** üzerinde gün bazlı aralık, **her iki uç dâhil**. Tarih filtresi
  verildiğinde **taslaklar listelenmez** (taslağın fatura tarihi yoktur).
- `search`: **fatura numarası** veya **misafir adı/soyadında** contains (büyük-küçük harf duyarsız)
- Sıralama: `issuedAt` tersine; taslakta tarih olmadığı için `createdAt`'e düşülür
  (`COALESCE(issuedAt, createdAt) DESC`, eşitlikte `id`).

#### Tutar hesabı (sunucuda — istemci toplamlarına güvenilmez)

1. **Birim fiyatlar BRÜTtür (KDV dâhil).** KDV satırdan *içinden çıkarılır*:
   `lineGross = round(quantity × unitPrice)`, `lineNet = round(lineGross / (1 + oran))`,
   `lineVat = lineGross − lineNet`. Böylece `lineNet + lineVat == lineGross` **her zaman** tutar.
   Gerekçe: DE'de tüketiciye gösterilen fiyat brüt son fiyattır (PAngV) ve
   `Reservation.TotalAmount` domainde "toplam brüt" olarak tanımlıdır.
2. **KDV oranı eşlemesi** (`Hotel.TaxProfile` — koda hardcode YOK):
   | Satır türü | Oran | Gerekçe |
   |---|---|---|
   | `RoomCharge` | `reducedVatRate` (DE: %7) | Konaklama hizmeti — UStG §12 Abs. 2 Nr. 11 |
   | `Extra` | `vatRate` (DE: %19) | Kahvaltı/minibar/otopark; kahvaltı indirimli orandan yararlanmaz (Aufteilungsgebot) |
   | `CityTax` | **%0** | Kurtaxe belediyenin misafirden aldığı vergidir; otel yalnızca tahsil eder (durchlaufender Posten) → KDV matrahına girmez |
3. **Kurtaxe** otelde etkinse (`cityTaxEnabled`) rezervasyon yolunda otomatik eklenir:
   `quantity = vergiye tabi kişi × gece`, `unitPrice = cityTaxPerPersonNight`,
   `type = CityTax`, `vatRate = 0`. `cityTaxAmount` ayrı toplam olarak döner ve
   **`netAmount`'a dâhil edilmez**.
   **Vergiye tabi kişi sayısı otelin çocuk muafiyetine bağlıdır** (`Hotel.TaxProfile`):
   | `cityTaxExemptChildren` | Vergiye tabi kişi | 2 yetişkin + 2 çocuk × 2 gece, 3,00 €/kişi/gece |
   |---|---|---|
   | `false` (varsayılan) | `adults + children` | `quantity = 8` → **24,00 €** |
   | `true` | `adults` (çocuklar sayılmaz) | `quantity = 4` → **12,00 €** |
   - **Yaş sınırı hesaba GİRMEZ:** rezervasyonda misafir doğum tarihi tutulmaz; muafiyet
     "çocuk olarak girilen kişiler" kümesine uygulanır. `cityTaxChildAgeLimit` yalnızca
     satır açıklamasına yazılır (muafiyetin dayanağı, Kurtaxe beyanı için):
     `"City tax (Kurtaxe) 2 person(s) x 2 night(s) - children under 18 exempt"`.
     Sınır `null` ise açıklama `"... - children exempt"` olur.
   - Muafiyet açık ve vergiye tabi kişi kalmadıysa (yalnızca çocuk) **Kurtaxe satırı hiç
     üretilmez** (sıfır tutarlı kalem yazılmaz).
   - Muafiyet **opt-in**'dir: alan varsayılan `false` olduğu için mevcut oteller etkilenmez.
     Otel muafiyeti açtığında **çocuklu rezervasyonların Kurtaxe tutarı düşer** — bu bilinçli
     bir davranış değişikliğidir. Alanlar `PUT /hotels/{id}/settings` ile yönetilir.
4. **Yuvarlama:** 2 ondalık, **kaufmännisch** (yarım yukarı) ve **satır bazında**. Fatura
   toplamları yuvarlanmış satır tutarlarının toplamıdır (yazdırılan satırlar toplamla birebir
   uyuşur). Negatif tutarlarda simetriktir → storno orijinali **tam olarak** sıfırlar.
5. İstemci `vatRate`, `lineNet`, `lineVat` veya fatura toplamı **gönderemez** (alanlar
   sözleşmede yoktur) — vergi matrahı manipüle edilemez.

#### Fatura numarası (GoBD §6.2 — kesintisiz sekans)

- Biçim: **`{yıl}-{6 hane}`** → `2026-000001`. Sekans **otel + yıl** bazındadır ve her yıl 1'den
  başlar (`HotelInvoiceCounter`).
- Numara **yalnızca finalize anında** atanır. Taslakta `invoiceNumber = null` döner; terk edilen
  taslaklar sekansta **boşluk bırakmaz**.
- Sayaç artışı ile faturanın numara/tarih/durum değişikliği **tek transaction**tadır.
- **Eşzamanlılık:** aynı otel/yıl sayacını aynı anda güncelleyen ikinci istek **409** alır ve
  **hiçbir numara tüketilmez** (transaction tümüyle geri alınır); istek tekrarlanabilir.
  İki bağımsız koruma vardır: `HotelInvoiceCounter.Version` optimistic concurrency token'ı ve
  `Invoice(HotelId, InvoiceNumber)` unique index'i — hangisi önce tetiklerse sonuç aynıdır:
  **ne tekrar ne atlama**.

#### İptal / Stornorechnung (GoBD §6.1)

- **Draft iptali:** durum doğrudan `Cancelled` olur, **iptal faturası oluşturulmaz** (numarası
  olmayan taslak belge değildir; sekansta boşluk doğmaz). Folio kaynaklı satırlar folio'ya geri
  bırakılır (masraf kaybolmaz).
- **Finalized/Paid iptali:** orijinal **korunur** (satırları, numarası ve tutarları dâhil) ve
  `status = Cancelled`, `cancelledByInvoiceId = <storno>` olur. Yeni bir **Stornorechnung**
  kesilir: kendi numarasını alır, satırları orijinalin **aynası**dır (`"Storno: " + açıklama`,
  negatif birim fiyat, negatif `lineNet`/`lineVat`) ve tutarları orijinalin negatifidir.
  Böylece orijinal + storno = **0**.
- İptal faturası, orijinali `cancelsInvoiceId` alanında gösterir. Çiftin **iki yönü de** saklanır
  (`orijinal.cancelledByInvoiceId` ⇔ `storno.cancelsInvoiceId`) ve tek domain çağrısında birlikte
  kurulur; yanıt türetilmiş bir alt sorgudan değil doğrudan kolondan gelir.
- Zaten iptal edilmiş fatura → **409**.

#### Ödeme ve `Paid`

- Ödeme **yalnızca `Finalized`** faturaya kaydedilir. `Draft` → **409** ("önce finalize"),
  `Cancelled` → 409, `Paid` (tamamen ödenmiş) → 409.
- **Kısmi ödeme** serbesttir; durum brüt tutara ulaşana kadar `Finalized` kalır.
  `paidAmount`/`outstandingAmount` her yanıtta güncel gelir. Her ödeme denetim izine
  `PaymentRecorded` olarak yazılır; bakiyeyi kapatan ödemede ayrıca `Paid` kaydı oluşur.
- **Fazla ödeme → 409** (kuruş toleransı yoktur): fatura tutarını aşan tahsilat faturanın
  parçası değildir.
- Brüt tutarı **≤ 0** olan belgeye (Stornorechnung) ödeme kaydedilemez → 409;
  **iade akışı bu fazda yok**.

#### Denetim izi (GoBD §6.3)

- Her işlem **append-only** yazılır: kim (`performedByUserId`), ne zaman (`performedAt`, UTC),
  ne (`details`, JSON).
- Denetim kaydı, tetikleyen işlemle **aynı `SaveChanges`** (aynı transaction) içinde yazılır →
  "iz olmadan işlem" veya "işlem olmadan iz" oluşamaz.

| `action` | Ne zaman | `details` (özet) |
|---|---|---|
| `Created` | Taslak veya Stornorechnung oluşturuldu | kaynak (manual/reservation), satır sayısı, tutarlar, uygulanan oranlar |
| `Updated` | **Taslak** güncellendi (`PUT /invoices/{id}`) | `changedFields` + `guestId/culture/lineCount` ve `net/vat/cityTax/gross` için `{old,new}` |
| `Finalized` | Numara atandı, belge kilitlendi | `invoiceNumber`, `issuedAt`, tutarlar, satır sayısı |
| `PaymentRecorded` | **Her** ödeme (kısmi dâhil) | `paymentId`, `method`, `amount`, `paidAt`, `reference`, `totalPaid`, `outstandingAmount` |
| `Paid` | **Yalnızca** bakiye kapandığında (durum geçişi) | `previousStatus`, `status`, `settledByPaymentId`, `totalPaid` |
| `Cancelled` | Taslak iptali veya Stornorechnung ile iptal | önceki durum, `stornoRequired`, `cancelledByInvoiceId`, `reason` |

- **`PaymentRecorded` ile `Paid` ayrıdır:** ilki bir *tahsilat olayı*, ikincisi bir *durum
  geçişi*dir. Bakiyeyi kapatan ödemede **iki kayıt** oluşur (`PaymentRecorded`, ardından `Paid`);
  kısmi ödemede yalnızca `PaymentRecorded`. Eski `details.fullySettled` ayrımı **kaldırıldı**.
- `Updated` GoBD açısından **zorunlu değildir** (taslak henüz belge değildir), *Nachvollziehbarkeit*
  için tutulur: faturanın hangi tutarla oluşup hangi tutarla kesinleştiği izlenebilir olur.

#### Doğrulama (400 + `errors`)

- `reservationId` ve `lineItems` **birbirini dışlar**: ikisi birden → 400, hiçbiri → 400.
- `reservationId` yoksa `guestId` zorunlu; misafir/rezervasyon bulunamazsa **404**
  (başka otelin kaydı da "bulunamadı" sayılır — varlık sızdırılmaz).
- `culture` ∈ `de|en|tr` · `lineItems` 1–200 öğe · `description` zorunlu ≤ 500 ·
  `quantity` > 0 ve ≤ 9999 · `unitPrice` ≥ 0 ve ≤ 1.000.000 · `type` ∈ `RoomCharge|Extra|CityTax`
- `amount` > 0 ve ≤ 1.000.000 · `paidAt` gelecekte olamaz · `reference` ≤ 128 · `reason` ≤ 500
- **Negatif miktar/fiyat istemciden kabul edilmez**; eksi tutarı yalnızca sunucu (storno) üretir.
- **Yazma işlemleri aktif otel gerektirir:** `POST /invoices` çağrısında Head Office kullanıcısı
  `X-Hotel-Id` göndermezse **400** (`errors: { "X-Hotel-Id": [...] }`). Mevcut bir faturaya
  yapılan işlemlerde (finalize/cancel/payments) otel **faturadan** okunur, header zorunlu değildir.

#### Rezervasyondan üretim kuralları

- Oda ücreti: `gece × gecelik fiyat`; fiyat kaynağı sırayla `RatePlan.Price` → `RoomType.BasePrice`.
  `Reservation.TotalAmount` **kullanılmaz** (kalem bazlı belge gerekli; toplam ekstra/indirim
  içerebilir). Gece sayısı 0 ise (aynı gün giriş-çıkış) **1 gece** hesaplanır.
- Ekstralar: folio'nun **henüz faturalanmamış** satırları (`folioId` dolu, `invoiceId` boş)
  faturaya **taşınır** (`folioId` korunur) → aynı masraf iki kez faturalanamaz. Satır tutarları
  sunucuda **yeniden hesaplanır**; satırdaki oran > 0 ise korunur, değilse türden çözülür.
- Aynı rezervasyon için **iptal edilmemiş** bir fatura varsa → **409**.
- Kurtaxe: `type = CityTax`, kişi sayısı otelin çocuk muafiyetine göre belirlenir
  (bkz. "Tutar hesabı" §3). Muafiyet açıkken açıklamaya muafiyet notu eklenir.
- Satır açıklamaları şu an **dil-nötr ASCII** üretilir (`"Room charge ..."`, `"City tax (Kurtaxe) ..."`);
  yerelleştirme fatura PDF/exporter fazında yapılacaktır.

#### PDF / e-fatura

`GET /invoices/{id}/pdf` → **501 Not Implemented** (`ProblemDetails`). Sahte/boş PDF
döndürülmez. Üretim portu `IInvoiceExporter` olarak tanımlıdır (`Pdf`, `ZugferdPdfA3`,
`XRechnungXml`) ancak DI'a kayıtlı implementasyonu yoktur; fatura verisi
`GET /invoices/{id}` ile yapılandırılmış biçimde alınabilir (architecture.md §6.5).

#### Hata kodları özeti

| Durum | Kod |
|---|---|
| Doğrulama, aktif otel yok, gelecek `paidAt` | 400 |
| Token yok/geçersiz | 401 |
| İzin yok (`Invoices.Approve` olmadan finalize vb.) | 403 |
| Fatura/misafir/rezervasyon/otel bulunamadı (veya başka otelin) | 404 |
| Kesinleşmiş faturayı düzenleme, taslak olmayan faturayı finalize, ikinci kez iptal, fazla ödeme, taslağa ödeme, aynı rezervasyona ikinci fatura, numara sekansı yarışı | 409 |
| PDF üretimi | 501 |
