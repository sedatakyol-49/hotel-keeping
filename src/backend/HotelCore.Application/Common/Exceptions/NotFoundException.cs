using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Common.Exceptions;

/// <summary>
/// İstenen kayıt bulunamadı. Api katmanında <b>404 Not Found</b>'a maplenir.
/// <para>
/// Multi-tenant not: tenant filtresi yüzünden "başka otelin kaydı" da bulunamamış sayılır —
/// bu kasıtlıdır, varlığın var olduğu bilgisi sızdırılmaz.
/// </para>
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
        : base(Messages.NotFoundTitle)
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotFoundException(string entityName, object key)
        : base(Messages.EntityNotFound(entityName, key))
    {
        EntityName = entityName;
    }

    public string? EntityName { get; }
}
