namespace HotelCore.Application.Common.Messaging;

/// <summary>
/// Bir use-case girdisi (command veya query). Yanıt tipi <typeparamref name="TResponse"/>
/// ile taşındığı için <see cref="IDispatcher"/> handler'ı derleme zamanı bilgisiyle çözebilir.
/// <para>
/// MediatR ticari lisansa geçtiği için (architecture.md §2) bu ince soyutlama kendi kodumuzdur.
/// </para>
/// </summary>
/// <typeparam name="TResponse">Handler'ın döndüreceği tip.</typeparam>
#pragma warning disable CA1040 // İşaretleyici arayüz kasıtlı: yanıt tipini taşımak dışında üye gerekmez.
public interface IRequest<out TResponse>;
#pragma warning restore CA1040
