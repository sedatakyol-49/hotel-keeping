using HotelCore.Application.Common.Interfaces;

namespace HotelCore.Infrastructure.Security;

/// <summary>
/// BCrypt tabanlı parola özetleme (BCrypt.Net-Next). Salt her hash'te otomatik üretilir ve
/// özetin içinde saklanır; ayrı bir salt kolonu gerekmez.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// BCrypt work factor. 12 ≈ 250 ms/hash (2025 donanımı) — brute-force maliyetini yükseltir,
    /// login gecikmesini kabul edilebilir tutar. Artırıldığında eski hash'ler çalışmaya devam eder
    /// (work factor özetin içinde kodludur).
    /// </summary>
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Bozuk/eski formatta özet: doğrulama başarısız sayılır, istisna dışarı sızmaz.
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
