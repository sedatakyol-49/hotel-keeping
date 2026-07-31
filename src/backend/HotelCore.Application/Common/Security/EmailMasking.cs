using System.Globalization;

namespace HotelCore.Application.Common.Security;

/// <summary>
/// E-posta adresinin log ve yanıt için maskelenmesi (<c>juergen@example.de</c> →
/// <c>j***@e***.de</c>).
/// <para>
/// <b>Neden ayrı ve public:</b> maskeleme hem Application handler'larında (yanıttaki
/// <c>confirmation.recipientMasked</c>) hem Api'deki gönderici implementasyonunda (log) gerekir.
/// İki ayrı maskeleme, birinin daha az maskelemesi riskini taşırdı.
/// </para>
/// </summary>
public static class EmailMasking
{
    /// <summary>Adresi maskeler; boş/geçersiz değerde bilgi taşımayan bir yer tutucu döner.</summary>
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at == email.Length - 1)
        {
            return "***";
        }

        var local = email[..at];
        var domain = email[(at + 1)..];
        var dot = domain.LastIndexOf('.');

        var maskedDomain = dot > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{domain[0]}***{domain[dot..]}")
            : string.Create(CultureInfo.InvariantCulture, $"{domain[0]}***");

        return string.Create(CultureInfo.InvariantCulture, $"{local[0]}***@{maskedDomain}");
    }
}
