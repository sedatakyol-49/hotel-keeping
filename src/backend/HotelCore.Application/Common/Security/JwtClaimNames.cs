namespace HotelCore.Application.Common.Security;

/// <summary>
/// JWT claim adları — api-contracts.md "JWT Claim Şeması" bölümünün koddaki tek kaynağı.
/// Token üretimi (Infrastructure), okuma (<c>ICurrentUser</c>) ve policy kaydı (Api)
/// bu sabitleri kullanır; hiçbir yerde metin olarak tekrarlanmaz.
/// </summary>
public static class JwtClaimNames
{
    /// <summary>Kullanıcı kimliği (standart <c>sub</c>).</summary>
    public const string Subject = "sub";

    public const string Email = "email";

    public const string HeadOfficeId = "headOfficeId";

    /// <summary>Çoklu claim — her izin anahtarı için bir değer.</summary>
    public const string Permission = "perm";

    /// <summary>Çoklu claim — erişilebilir otel id'leri. İlk değer varsayılan oteldir.</summary>
    public const string Hotel = "hotel";

    /// <summary>"true" ise otel filtresi bypass edilir (Head Office konsolide görünüm).</summary>
    public const string AllHotels = "allHotels";

    public const string Culture = "culture";
}
