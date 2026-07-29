namespace HotelCore.Application.Common.Messaging;

/// <summary>
/// Bir use-case'in gövdesi. Her istek tipi için tek handler bulunur (vertical slice).
/// </summary>
/// <typeparam name="TRequest">İstek tipi.</typeparam>
/// <typeparam name="TResponse">Yanıt tipi.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
