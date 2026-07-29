namespace HotelCore.Domain.Enums;

/// <summary>Zaman kaydının kaynağı. Bu fazda yalnızca <see cref="Manual"/> (web) kullanılır.</summary>
public enum TimeEntrySource
{
    Manual = 0,
    Terminal = 1,
    Import = 2
}
