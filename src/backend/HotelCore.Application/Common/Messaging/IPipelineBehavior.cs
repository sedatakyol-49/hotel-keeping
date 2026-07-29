namespace HotelCore.Application.Common.Messaging;

/// <summary>
/// Boru hattındaki bir sonraki adımı (davranış veya handler) temsil eder.
/// </summary>
/// <typeparam name="TResponse">Yanıt tipi.</typeparam>
public delegate Task<TResponse> PipelineContinuation<TResponse>();

/// <summary>
/// Handler çevresinde çalışan çapraz kesen davranış (validation, logging, ...).
/// Açık generic olarak kaydedilir; kayıt sırası dıştan içe boru hattı sırasıdır.
/// </summary>
/// <typeparam name="TRequest">İstek tipi.</typeparam>
/// <typeparam name="TResponse">Yanıt tipi.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Davranışı çalıştırır. <paramref name="continuation"/> çağrılmazsa boru hattı kısa devre olur.
    /// </summary>
    Task<TResponse> Handle(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken);
}
