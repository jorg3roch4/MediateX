using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediateX.Validation;

namespace MediateX.Behaviors;

/// <summary>
/// Pipeline behavior that executes all registered validators for a request.
/// If any validation fails, throws a <see cref="ValidationException"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request being validated.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IRequestValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();

        if (validators.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var validationTasks = validators
            .Select(v => v.ValidateAsync(request, cancellationToken));

        var results = await Task.WhenAll(validationTasks.Select(vt => vt.AsTask())).ConfigureAwait(false);

        var errors = results
            .Where(r => r.IsInvalid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Pipeline behavior that executes validators and returns a <see cref="Result{T}"/> on failure
/// instead of throwing an exception. Use this for handlers that return <see cref="Result{T}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request being validated.</typeparam>
/// <typeparam name="TValue">The type of value in the Result.</typeparam>
public class ValidationResultBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestValidator<TRequest>> _validators;

    public ValidationResultBehavior(IEnumerable<IRequestValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<Result<TValue>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue>> next,
        CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();

        if (validators.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var validationTasks = validators
            .Select(v => v.ValidateAsync(request, cancellationToken));

        var results = await Task.WhenAll(validationTasks.Select(vt => vt.AsTask())).ConfigureAwait(false);

        var errors = results
            .Where(r => r.IsInvalid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (errors.Count > 0)
        {
            var firstError = errors[0];
            return Result<TValue>.Failure(
                firstError.ErrorCode ?? "Validation",
                errors.Count == 1
                    ? firstError.ErrorMessage
                    : $"{errors.Count} validation errors: {string.Join("; ", errors.Take(3).Select(e => e.ErrorMessage))}");
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Pipeline behavior for void Result that executes validators and returns failure on validation errors.
/// </summary>
/// <typeparam name="TRequest">The type of request being validated.</typeparam>
public class ValidationResultVoidBehavior<TRequest> : IPipelineBehavior<TRequest, Result>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestValidator<TRequest>> _validators;

    public ValidationResultVoidBehavior(IEnumerable<IRequestValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<Result> Handle(
        TRequest request,
        RequestHandlerDelegate<Result> next,
        CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();

        if (validators.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var validationTasks = validators
            .Select(v => v.ValidateAsync(request, cancellationToken));

        var results = await Task.WhenAll(validationTasks.Select(vt => vt.AsTask())).ConfigureAwait(false);

        var errors = results
            .Where(r => r.IsInvalid)
            .SelectMany(r => r.Errors)
            .ToList();

        if (errors.Count > 0)
        {
            var firstError = errors[0];
            return Result.Failure(
                firstError.ErrorCode ?? "Validation",
                errors.Count == 1
                    ? firstError.ErrorMessage
                    : $"{errors.Count} validation errors: {string.Join("; ", errors.Take(3).Select(e => e.ErrorMessage))}");
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
