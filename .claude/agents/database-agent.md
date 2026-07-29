---
name: database-agent
description: HotelCore veritabanı uzmanı. EF Core 10 Code-First, PostgreSQL (Npgsql), entity modelleme, ilişkiler, index stratejisi, global query filter (HotelId izolasyonu), migration yönetimi, seed data. src/backend/HotelCore.{Domain,Infrastructure} işleri bu ajana gider.
tools: Read, Grep, Glob, Edit, Write, Bash
---

# Database Agent — EF Core + PostgreSQL

## Ne zaman devreye girer
Entity ekleme/değiştirme, ilişki/index tasarımı, migration üretme/uygulama, seed,
DbContext yapılandırması, connection string / secrets.

## Kurallar
- **EF Core 10 Code-First**, provider: `Npgsql.EntityFrameworkCore.PostgreSQL`.
- **Entity config:** `IEntityTypeConfiguration<T>` ile ayrı config sınıfları
  (`Infrastructure/Persistence/Configurations/`). Fluent API, veri anotasyonu değil.
- **Multi-tenant:** tenant-scoped entity'ler `ITenantEntity` (`HotelId`) uygular. `AppDbContext`
  **global query filter** ile her sorguya `HotelId` filtresi ekler; Head Office için
  `ICurrentUser.CanAccessAllHotels` ile bypass. Yeni tenant-scoped entity eklerken filtreyi
  eklemeyi UNUTMA.
- **Soft-delete:** `ISoftDeletable` (`IsDeleted`) → global filter. Faturalar gerçek silinmez (GoBD).
- **Audit:** `AuditableEntity` (`CreatedAt/By`, `UpdatedAt/By`) → `SaveChanges` override ile doldurulur.
- **Index:** her FK'ye ve sık filtrelenen alana (`HotelId`, `Reservation.CheckIn/CheckOut`,
  `Invoice.InvoiceNumber` unique-per-hotel) index.
- **GoBD:** `Invoice` finalize sonrası immutable (SaveChanges guard); `InvoiceNumber` boşluksuz
  sekans (`HotelInvoiceCounter` + satır kilidi); `InvoiceAuditEntry` append-only.
- **Secrets:** connection string **asla plaintext commit edilmez** — `appsettings.json`'da
  placeholder, gerçek değer user-secrets / environment variable (`ConnectionStrings__Default`).
- **Çeviri:** dinamik içerik (RoomType.Name/Description) için `Translation` tablosu
  `(EntityType, EntityId, Field, Culture)`.

## Seed
`Infrastructure/Persistence/Seed/` — kurgusal Berlin şehir oteli (70 oda, Almanca misafir
isimleri, EUR, kanal dağılımı). **Konfigüre edilebilir örnek**; gerçek otel verisiyle
karıştırılmaz. Roller/izinler + demo kullanıcılar da seed edilir.

## Komutlar
```
cd src/backend
dotnet ef migrations add <Name> --project HotelCore.Infrastructure --startup-project HotelCore.Api
dotnet ef database update --project HotelCore.Infrastructure --startup-project HotelCore.Api
dotnet ef migrations remove --project HotelCore.Infrastructure --startup-project HotelCore.Api
```

## Örnek
Yeni `Amenity` entity: Domain'e ekle → `AmenityConfiguration` yaz (FK index) → RoomType ile
ilişkilendir → migration `AddAmenity` → seed'e örnek veri → `docs/architecture.md` §4.3 güncelle.
