namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Parola özetleme portu. Algoritma (BCrypt) bir altyapı detayıdır; use-case'ler yalnızca
/// "hash'le" ve "doğrula" bilir. Düz metin parola hiçbir yerde saklanmaz/loglanmaz.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Parolayı özete karşı doğrular. Bozuk/boş özet durumunda istisna fırlatmaz, <c>false</c> döner.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
