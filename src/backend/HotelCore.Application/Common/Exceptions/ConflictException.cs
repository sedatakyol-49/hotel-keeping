namespace HotelCore.Application.Common.Exceptions;

/// <summary>
/// İş kuralı çakışması (örn. dolu oda, kesinleşmiş fatura, tekrarlanan kayıt).
/// Api katmanında <b>409 Conflict</b>'e maplenir.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException()
        : base("Islem mevcut durumla celisiyor.")
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
