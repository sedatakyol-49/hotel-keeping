using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests;

/// <summary>
/// CI guvenlik agi: integration testlerin <b>sessizce atlanmadigini</b> dogrular.
/// <para>
/// <see cref="RequiresPostgresFactAttribute"/> kaynak yoksa testleri "skipped" olarak isaretler.
/// Bu yerelde (Docker'i olmayan gelistirici) dogru davranistir, fakat CI'da bir yapilandirma
/// hatasi — ornegin <c>ConnectionStrings__Default</c>'un dusmesi — tum integration kapsaminin
/// sessizce kaybolmasina ve is akisinin yine yesil kalmasina yol acardi.
/// </para>
/// <para>
/// Bu yuzden CI is akisi <c>HOTELCORE_REQUIRE_POSTGRES=true</c> gonderir; asagidaki test o
/// durumda kaynagin gercekten erisilebilir oldugunu <b>zorunlu</b> kilar.
/// </para>
/// </summary>
public sealed class PostgresAvailabilityTests
{
    [Fact]
    public void Integration_tests_are_not_silently_skipped_when_a_database_is_required()
    {
        if (!DatabaseAvailability.IsDatabaseRequired)
        {
            // Yerel kosu: atlama mekanizmasi mesrudur, zorlama yapilmaz.
            DatabaseAvailability.SkipReason.Should().NotBeEmpty("atlama gerekcesi aciklayici olmalidir");
            return;
        }

        DatabaseAvailability.IsAvailable.Should().BeTrue(
            "{0}=true iken PostgreSQL zorunludur; '{1}' ortam degiskeni verilmeli (CI service container) " +
            "veya Docker erisilebilir olmalidir. Aksi halde integration testler sessizce atlanirdi.",
            DatabaseAvailability.RequireDatabaseEnvironmentVariable,
            DatabaseAvailability.ConnectionStringEnvironmentVariable);
    }
}
