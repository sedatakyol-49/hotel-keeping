namespace HotelCore.Domain.Common;

/// <summary>
/// Tüm kök entity'ler için ortak taban tip. Kimlik olarak <see cref="Guid"/> kullanılır
/// (dağıtık üretim + tenant sızıntısı riskini azaltan tahmin edilemez anahtar).
/// Composite anahtarlı ilişki tabloları (RolePermission, UserRole, UserHotelAccess) bu tipten türemez.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Birincil anahtar. Uygulama tarafında üretilir (DB sequence değil).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
