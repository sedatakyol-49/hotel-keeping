namespace HotelCore.Domain.Enums;

/// <summary>
/// ISO 3166-1 alpha-2 ülke kodları. Vergi oranları buraya bağlı DEĞİLDİR —
/// oranlar otel bazında <c>TaxProfile</c> üzerinden yönetilir (architecture.md §4.1).
/// GoBD kuralları bu fazda yalnızca <see cref="DE"/> için zorunludur.
/// </summary>
public enum Country
{
    DE = 0,
    AT = 1,
    CH = 2,
    TR = 3,
    NL = 4,
    FR = 5,
    IT = 6,
    ES = 7,
    GB = 8,
    US = 9
}
