using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Common.Exceptions;

/// <summary>
/// Kimlik doğrulanmış ancak işlem için yetkisiz (izin veya otel erişimi yok).
/// Api katmanında <b>403 Forbidden</b>'a maplenir.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException()
        : base(Messages.ForbiddenDefault)
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
