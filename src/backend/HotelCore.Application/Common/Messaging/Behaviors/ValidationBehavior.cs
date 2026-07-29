using FluentValidation;
using ValidationException = HotelCore.Application.Common.Exceptions.ValidationException;

namespace HotelCore.Application.Common.Messaging.Behaviors;

/// <summary>
/// İstek handler'a ulaşmadan önce kayıtlı tüm FluentValidation validator'larını çalıştırır.
/// Hata varsa alan bazlı sözlükle <see cref="ValidationException"/> fırlatır (Api'de 400 + errors).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var applicable = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (applicable.Length == 0)
        {
            return await continuation().ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in applicable)
        {
            var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures
                .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal));
        }

        return await continuation().ConfigureAwait(false);
    }
}
