namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Tum integration test siniflari ayni PostgreSQL ornegini paylasir
/// (konteyner/servis baslatma maliyeti bir kez odenir).
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
