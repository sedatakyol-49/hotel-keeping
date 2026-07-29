namespace HotelCore.Application.Common.Messaging;

/// <summary>
/// İsteği ilgili handler'a (boru hattından geçirerek) yönlendirir.
/// Controller'lar yalnızca bu arayüzü tanır; handler tiplerini bilmez.
/// </summary>
public interface IDispatcher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
