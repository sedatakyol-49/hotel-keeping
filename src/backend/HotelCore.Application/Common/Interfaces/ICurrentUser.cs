namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Aktif isteğin kimlik bağlamı (JWT claim'lerinden okunur — bkz. api-contracts.md "JWT Claim Şeması").
/// Infrastructure bu arayüzü TÜKETİR (global query filter + audit alanları), implementasyon Api katmanındadır.
/// Kimlik doğrulanmamışsa <see cref="UserId"/> ve <see cref="HotelId"/> null,
/// <see cref="CanAccessAllHotels"/> false olur; bu durumda hiçbir tenant satırı görünmez.
/// </summary>
public interface ICurrentUser
{
    /// <summary>JWT "sub" claim'i.</summary>
    Guid? UserId { get; }

    /// <summary>Aktif otel (X-Hotel-Id header'ı veya kullanıcının varsayılan oteli).</summary>
    Guid? HotelId { get; }

    /// <summary>JWT "allHotels" claim'i — Head Office konsolide görünümü.</summary>
    bool CanAccessAllHotels { get; }

    /// <summary>JWT "headOfficeId" claim'i.</summary>
    Guid? HeadOfficeId { get; }

    /// <summary>JWT "perm" claim'leri (izin anahtarları).</summary>
    IReadOnlyCollection<string> Permissions { get; }

    bool IsAuthenticated { get; }
}
