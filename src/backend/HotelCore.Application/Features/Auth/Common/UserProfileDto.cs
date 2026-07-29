namespace HotelCore.Application.Features.Auth.Common;

/// <summary>
/// Oturum açmış kullanıcının profili: kimlik + roller + düz izin listesi + erişilebilir oteller.
/// <para>
/// <b>Sözleşme:</b> <c>GET /api/v1/auth/me</c> bu nesneyi sarmalayıcısız döner;
/// <c>POST /api/v1/auth/login</c> ise <c>user</c> alanı olarak. Frontend bu şekle bağlıdır
/// (özellik sırası ve adları değiştirilmemelidir).
/// </para>
/// </summary>
public sealed record UserProfileDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Görünen ad. Domain'de ayrı bir alan yoktur; ileride eklenene kadar <c>null</c> döner
    /// (frontend null durumunda ad+soyad birleştirir).
    /// </summary>
    public string? DisplayName { get; init; }

    public string Culture { get; init; } = "de";

    public Guid HeadOfficeId { get; init; }

    /// <summary>Rol adları — bilgi amaçlıdır; yetki kontrolü <see cref="Permissions"/> ile yapılır.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Düz izin anahtarı listesi (<c>Modül.Aksiyon</c>), rollerin birleşimi.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Erişilebilir oteller (Head Office kullanıcısında head office'in tüm otelleri).</summary>
    public IReadOnlyList<HotelSummaryDto> Hotels { get; init; } = [];

    /// <summary>True ise otel filtresi bypass edilir (konsolide görünüm).</summary>
    public bool CanAccessAllHotels { get; init; }

    /// <summary>Giriş sonrası önerilen aktif otel; hiç otel erişimi yoksa null.</summary>
    public Guid? DefaultHotelId { get; init; }
}
