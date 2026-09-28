using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Cakra.Core;

/// <summary>
/// MediatR pipeline behavior that executes all FluentValidation validators for a
/// request before the handler runs (Architecture §19.2: "strongly typed request
/// validation executed automatically via MediatR pipeline behaviors prior to
/// handler execution"). When any validator reports a failure a
/// <see cref="ValidationException"/> is thrown and the handler is never invoked.
/// </summary>
/// <typeparam name="TRequest">MediatR request type being dispatched.</typeparam>
/// <typeparam name="TResponse">MediatR response type produced by the handler.</typeparam>
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Creates the behavior over every validator registered for <typeparamref name="TRequest"/>.</summary>
    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = new List<ValidationFailure>();

            foreach (var validator in _validators)
            {
                var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
                if (!result.IsValid)
                {
                    failures.AddRange(result.Errors);
                }
            }

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next().ConfigureAwait(false);
    }
}
