namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="RequiresPostgresFactAttribute"/>'un veri guden (data-driven) karsiligi: ayni
/// senaryonun birden cok girdiyle kosmasi gerektiginde kullanilir (orn. <c>de|en|tr</c>).
/// Kaynak yoksa test "skipped" olarak raporlanir, BASARISIZ olmaz.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RequiresPostgresTheoryAttribute : TheoryAttribute
{
    public RequiresPostgresTheoryAttribute()
    {
        if (!DatabaseAvailability.IsAvailable)
        {
            Skip = DatabaseAvailability.SkipReason;
        }
    }
}
