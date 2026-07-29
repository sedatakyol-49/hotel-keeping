---
name: api-integration
description: Frontend↔backend API sözleşmesi (OpenAPI/Swagger) tutarlılığı, tip-güvenli client üretimi, DTO/endpoint/enum isimlendirme standardı. Sözleşme değişince tetiklenir.
---

# Skill: API Integration (contract-first)

## Tetikleyici senaryolar
Yeni/değişen endpoint, DTO şekli değişimi, enum ekleme, frontend client yenileme.

## İş akışı
1. Backend endpoint + DTO değişir → Swagger otomatik güncellenir.
2. `docs/api-contracts.md` insan-okunur özet güncellenir.
3. Frontend client yeniden üretilir (backend ayaktayken):
   ```
   cd src/frontend
   npx ng-openapi-gen --input http://localhost:5080/swagger/v1/swagger.json --output src/app/core/api
   ```
4. Frontend servis/store yeni tiplere uyarlanır.

## İsimlendirme standardı (Main Agent otoritesi)
- Endpoint: `kebab-case` yol, `/api/v1/...`.
- DTO: `PascalCase`, `...Request` / `...Response` / `...Dto` sonekleri.
- Enum değerleri: backend `PascalCase`, JSON'da string olarak serialize.
- Ortak hata formatı: `ProblemDetails`.

## Aktif otel & dil
- İstek header'ları: `Authorization: Bearer`, `X-Hotel-Id`, `Accept-Language`.
- Frontend interceptor bu header'ları otomatik ekler (auth interceptor + hotel-context interceptor).

## Doğrulama
Sözleşme değiştiğinde: backend build + frontend build ikisi de geçmeli; contract testleri
(varsa) koşulmalı. Uyumsuzlukta Main Agent standardı belirler.
