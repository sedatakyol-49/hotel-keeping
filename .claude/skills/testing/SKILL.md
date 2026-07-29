---
name: testing
description: HotelCore test standartları — backend xUnit + FluentAssertions + WebApplicationFactory, frontend Vitest + Playwright. Test yazma/çalıştırma ve CI davranışında tetiklenir.
---

# Skill: Testing

## Tetikleyici senaryolar
Yeni özellik için test, regresyon testi, CI'da test davranışı.

## Backend
- **Domain unit:** entity davranışı (örn. `Reservation.CheckIn()` durum geçişi).
- **Application:** handler testleri (in-memory `IAppDbContext` fake veya SQLite).
- **API integration:** `WebApplicationFactory<Program>` + gerçek PostgreSQL (CI service container).
  Auth + **tenant izolasyonu** (bir otelin kullanıcısı diğer otelin verisini görememeli) doğrulanır.
```csharp
[Fact]
public async Task Finalized_invoice_cannot_be_modified()
{
    // GoBD immutability guard testi
}
```

## Frontend
- **Vitest** unit: signal store aksiyonları, computed'ler, pipe/servisler.
- **Playwright** e2e: kritik akışlar (login, check-in, izin talebi, housekeeping güncelleme).

## Kritik test alanları (her zaman kapsanır)
- Multi-tenant izolasyon (HotelId filtresi + Head Office bypass).
- RBAC policy (izinsiz erişim 403).
- GoBD (finalize immutability, boşluksuz InvoiceNumber, audit entry).
- i18n (eksik çeviri anahtarı yok).

## Komutlar
```
cd src/backend && dotnet test
cd src/frontend && npm run test && npm run e2e
```

## CI
`.github/workflows/{backend,frontend}-ci.yml` — PR'da lint+build+test; PostgreSQL service
container ile integration. Başarısızsa merge engellenir (branch protection).
