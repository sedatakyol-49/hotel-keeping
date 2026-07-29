using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Refresh token (rotating / tek kullanımlık, sunucu tarafında iptal edilebilir).
/// Bu entity architecture.md §4.5'te yer almaz; ihtiyaç api-contracts.md'deki
/// <c>POST /api/v1/auth/refresh</c> sözleşmesinden gelmektedir.
/// <para>
/// Güvenlik: ham token DB'de SAKLANMAZ — yalnızca SHA-256 özeti (<see cref="TokenHash"/>)
/// tutulur, böylece veritabanı sızıntısında token'lar kullanılamaz.
/// </para>
/// <para>
/// Rotation: her yenilemede mevcut token iptal edilir (<see cref="RevokedAt"/>) ve
/// <see cref="ReplacedByTokenId"/> ile yenisine bağlanır. Zincir, iptal edilmiş bir token'ın
/// tekrar kullanılması hâlinde tüm ailenin iptal edilebilmesi (reuse detection) için tutulur.
/// </para>
/// <para>
/// Tenant-scoped DEĞİLDİR: kullanıcı aktif otel seçmeden token yenileyebilmelidir.
/// </para>
/// </summary>
public sealed class RefreshToken : EntityBase
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Ham token'ın SHA-256 özeti (hex/base64). Ham değer istemcide kalır.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Token'ı isteyen istemcinin IP adresi (IPv4/IPv6).</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Dolu ise token iptal edilmiştir (rotation veya logout).</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedByIp { get; set; }

    /// <summary>Rotation zinciri: bu token'ın yerine geçen token.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public RefreshToken? ReplacedByToken { get; set; }

    /// <summary>
    /// Hesaplanan değer — kolon olarak MAP EDİLMEZ (configuration'da Ignore edilir).
    /// Sorgularda kullanmak için koşulu doğrudan yazın: <c>RevokedAt == null &amp;&amp; ExpiresAt > now</c>.
    /// </summary>
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
