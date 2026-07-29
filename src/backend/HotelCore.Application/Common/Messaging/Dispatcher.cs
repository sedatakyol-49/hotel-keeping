using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Application.Common.Messaging;

/// <summary>
/// <see cref="IDispatcher"/> implementasyonu.
/// <para>
/// Çağıran yalnızca <c>IRequest&lt;TResponse&gt;</c> bilir; somut istek tipi çalışma zamanında
/// belli olur. Bu yüzden istek tipi başına bir <see cref="Invoker{TResponse}"/> sarmalayıcısı
/// üretilir (kapalı generic, reflection yalnızca ilk çağrıda) ve önbelleğe alınır. Sonraki
/// çağrılar sanal metot çağrısı kadar ucuzdur.
/// </para>
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> InvokerCache = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = (Invoker<TResponse>)InvokerCache.GetOrAdd(
            request.GetType(),
            static requestType => CreateInvoker<TResponse>(requestType));

        return invoker.Invoke(request, serviceProvider, cancellationToken);
    }

    private static object CreateInvoker<TResponse>(Type requestType)
    {
        var invokerType = typeof(Invoker<,>).MakeGenericType(requestType, typeof(TResponse));
        return Activator.CreateInstance(invokerType)
               ?? throw new InvalidOperationException($"Dispatcher invoker olusturulamadi: {requestType}.");
    }

    /// <summary>Somut istek tipini gizleyen non-generic köprü.</summary>
    private abstract class Invoker<TResponse>
    {
        public abstract Task<TResponse> Invoke(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class Invoker<TRequest, TResponse> : Invoker<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Invoke(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            var handler = serviceProvider.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"'{typeof(TRequest).Name}' icin IRequestHandler kaydi bulunamadi. " +
                    "AddApplication() cagrildigindan ve handler'in Application assembly'sinde oldugundan emin olun.");

            PipelineContinuation<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

            // Kayıt sırası dıştan içe olsun diye ters sarılır (ilk kaydedilen en dışta çalışır).
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
            for (var i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var next = pipeline;
                pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
            }

            return pipeline();
        }
    }
}
