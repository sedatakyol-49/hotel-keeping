---
name: database-migrations
description: EF Core 10 + PostgreSQL entity modelleme, IEntityTypeConfiguration, global query filter (HotelId), migration üretme/uygulama, seed. Entity değişikliği veya migration gerektiğinde tetiklenir.
---

# Skill: Database & Migrations

## Tetikleyici senaryolar
Entity ekleme/değiştirme, ilişki/index, global query filter, migration, seed.

## Yeni tenant-scoped entity ekleme adımları
1. `Domain/Entities/<Name>.cs` — `ITenantEntity` (+ gerekirse `AuditableEntity`/`ISoftDeletable`).
2. `Infrastructure/Persistence/Configurations/<Name>Configuration.cs` — Fluent API, FK index'leri.
3. `AppDbContext`'e `DbSet<Name>`; global query filter otomatik (base config tenant + soft-delete uygular).
4. Migration: `dotnet ef migrations add Add<Name> --project HotelCore.Infrastructure --startup-project HotelCore.Api`.
5. Gerekirse seed + `docs/architecture.md` §4 güncelle.

## Örnek config
```csharp
public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("rooms");
        b.HasKey(x => x.Id);
        b.Property(x => x.Number).HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.HotelId, x.Number }).IsUnique();
        b.HasOne(x => x.RoomType).WithMany(t => t.Rooms).HasForeignKey(x => x.RoomTypeId);
        b.HasIndex(x => x.HotelId);
    }
}
```

## Global query filter (AppDbContext)
Tenant + soft-delete filtresi merkezî uygulanır:
```csharp
// tenant: e.HotelId == _currentUser.HotelId || _currentUser.CanAccessAllHotels
// soft-delete: !e.IsDeleted
```

## GoBD notları
- `Invoice` finalize → immutable (SaveChanges guard).
- `InvoiceNumber` boşluksuz sekans (`HotelInvoiceCounter` + `FOR UPDATE`).
- Faturalar hard-delete edilmez.

## Secrets
Connection string user-secrets / env (`ConnectionStrings__Default`). Plaintext commit YASAK.

## Komutlar
```
dotnet ef migrations add <Name> --project HotelCore.Infrastructure --startup-project HotelCore.Api
dotnet ef database update       --project HotelCore.Infrastructure --startup-project HotelCore.Api
```
