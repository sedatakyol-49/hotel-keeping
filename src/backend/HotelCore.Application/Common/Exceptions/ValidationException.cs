using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Common.Exceptions;

/// <summary>
/// Girdi doğrulama hatası. Api katmanında <b>400 Bad Request</b> + RFC 7807
/// <c>ProblemDetails.errors</c> sözlüğüne maplenir.
/// </summary>
public sealed class ValidationException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> EmptyErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    public ValidationException()
        : this(EmptyErrors)
    {
    }

    public ValidationException(string message)
        : base(message)
    {
        Errors = EmptyErrors;
    }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = EmptyErrors;
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(Messages.ValidationDefault)
    {
        Errors = errors ?? EmptyErrors;
    }

    /// <summary>Alan adı → hata mesajları. ProblemDetails <c>errors</c> alanına birebir yazılır.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
