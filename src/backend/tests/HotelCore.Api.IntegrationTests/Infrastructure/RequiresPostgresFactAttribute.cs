namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// PostgreSQL gerektiren testler icin <see cref="FactAttribute"/> turevi.
/// xUnit v2'de kosullu atlama kesif (discovery) aninda <c>Skip</c> ozelligi set edilerek yapilir;
/// kaynak yoksa test "skipped" olarak raporlanir, BASARISIZ olmaz.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (!DatabaseAvailability.IsAvailable)
        {
            Skip = DatabaseAvailability.SkipReason;
        }
    }
}
