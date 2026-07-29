namespace HotelCore.Application.Common.Exceptions;

/// <summary>
/// Kimlik doğrulama başarısız (hatalı e-posta/parola, geçersiz veya iptal edilmiş refresh token).
/// Api katmanında <b>401 Unauthorized</b>'a maplenir.
/// <para>
/// Güvenlik: mesaj her zaman aynıdır — kullanıcının var olup olmadığı, parolanın mı yoksa
/// e-postanın mı yanlış olduğu <b>sızdırılmaz</b> (user enumeration).
/// </para>
/// </summary>
public sealed class AuthenticationException : Exception
{
    /// <summary>Tüm kimlik doğrulama hatalarında kullanılan tek tip mesaj.</summary>
    public const string GenericMessage = "E-posta veya parola hatali.";

    public AuthenticationException()
        : base(GenericMessage)
    {
    }

    public AuthenticationException(string message)
        : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
