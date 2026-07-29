namespace HotelCore.Domain.Entities;

/// <summary>
/// Kullanıcı ↔ Otel erişimi (bir bölge müdürü birden çok otele erişebilir).
/// Bu tablo tenant filtresine TABİ DEĞİLDİR: kullanıcının hangi otellere erişebileceği,
/// aktif otel seçilmeden önce okunmak zorundadır (login / hotel switcher).
/// </summary>
public sealed class UserHotelAccess
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    /// <summary>Kullanıcının giriş sonrası varsayılan oteli.</summary>
    public bool IsDefault { get; set; }
}
