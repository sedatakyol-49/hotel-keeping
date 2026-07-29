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

### Hotels & Ayarlar
| Method | Path | İzin |
|---|---|---|
| GET | `/hotels` | `Hotels.View` |
| GET | `/hotels/{id}` | `Hotels.View` |
| PUT | `/hotels/{id}/settings` | `Settings.Manage` |
| GET/PUT | `/head-office/settings` | `Settings.Manage` (marka adı, varsayılan politikalar) |

### Rooms & Housekeeping
| Method | Path | İzin |
|---|---|---|
| GET | `/room-types` | `Rooms.View` |
| GET | `/rooms` | `Rooms.View` |
| GET | `/rooms/board` | `Housekeeping.View` (kat bazlı pano) |
| PATCH | `/rooms/{id}/housekeeping` | `Housekeeping.Update` |
| GET | `/occupancy?from=&to=` | `Reservations.View` (doluluk grid'i) |

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
