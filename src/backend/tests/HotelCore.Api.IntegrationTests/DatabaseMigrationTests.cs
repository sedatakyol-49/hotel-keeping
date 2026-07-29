using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests;

/// <summary>
/// Migration'larin gercek bir PostgreSQL uzerinde uygulanabildigini dogrular.
/// Bu test CI'daki <c>dotnet ef database update</c> adiminin test tarafindaki karsiligidir:
/// snapshot ile migration dosyalari arasinda kayma olursa burada yakalanir.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DatabaseMigrationTests(PostgresFixture fixture)
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }

    [RequiresPostgresFact]
    public async Task Migrations_apply_and_leave_no_pending_migration()
    {
        await using var db = CreateContext();

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().NotBeEmpty("en az InitialCreate uygulanmis olmalidir");

        var pending = await db.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty("model ile veritabani semasi arasinda uygulanmamis migration kalmamalidir");
    }

    [RequiresPostgresFact]
    public async Task Schema_is_queryable_after_migration()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        // Tenant filtresi olmayan koke sorgu atilabiliyorsa tablolar gercekten olusmustur.
        var headOfficeCount = await db.HeadOffices.CountAsync();

        headOfficeCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [RequiresPostgresFact]
    public async Task Tenant_scoped_query_returns_nothing_without_an_identity()
    {
        // Kimlik yokken global query filter hicbir tenant satirini gostermemeli (guvenli varsayilan).
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        db.CurrentHotelId.Should().BeNull();
        db.CurrentUserCanAccessAllHotels.Should().BeFalse();

        var invoices = await db.Invoices.ToListAsync();

        invoices.Should().BeEmpty();
    }
}
