using HotelCore.Domain.Common;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Otel + yıl bazında boşluksuz fatura numarası sayacı (GoBD §6.2).
/// PostgreSQL sequence yerine tablo kullanılır: sequence rollback durumunda boşluk bırakır,
/// bu tablo ise transaction içinde satır kilidiyle (SELECT ... FOR UPDATE) artırıldığı için
/// atlama/tekrar oluşmaz. Ek koruma olarak <see cref="Version"/> optimistic concurrency
/// token'ı ile eşzamanlı güncellemeler tespit edilir.
/// </summary>
public sealed class HotelInvoiceCounter : EntityBase, ITenantEntity
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public int Year { get; set; }

    /// <summary>En son verilen sıra numarası. Bir sonraki fatura LastNumber + 1 alır.</summary>
    public int LastNumber { get; set; }

    /// <summary>Numara biçimi öneki (örn. 2026-) — fatura numarası: önek + sıfır dolgulu sayı.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Optimistic concurrency token. Değeri <c>AppDbContext.SaveChanges</c> içinde otomatik
    /// artırılır; iki istek aynı sayacı güncellemeye çalışırsa ikincisi
    /// <c>DbUpdateConcurrencyException</c> alır ve numara tekrarı önlenir.
    /// (PostgreSQL sistem kolonu xmin kullanılmadı: EF migration'ı sistem kolon adıyla
    /// çakışan gerçek bir kolon üretmeye çalışıyor.)
    /// </summary>
    public int Version { get; set; }
}
