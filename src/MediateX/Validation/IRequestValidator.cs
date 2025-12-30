using System.Threading;
using System.Threading.Tasks;

namespace MediateX.Validation;

/// <summary>
/// Defines a validator for a request.
/// </summary>
/// <typeparam name="TRequest">The type of request to validate.</typeparam>
public interface IRequestValidator<in TRequest> where TRequest : notnull
{
    /// <summary>
    /// Validates the specified request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result indicating success or failure with errors.</returns>
    ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a synchronous validator for a request.
/// Implement this interface for simple validation logic that doesn't require async operations.
/// </summary>
/// <typeparam name="TRequest">The type of request to validate.</typeparam>
public interface IRequestValidatorSync<in TRequest> where TRequest : notnull
{
    /// <summary>
    /// Validates the specified request synchronously.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>A validation result indicating success or failure with errors.</returns>
    ValidationResult Validate(TRequest request);
}

/// <summary>
/// Base class for implementing synchronous validators.
/// Automatically wraps the sync implementation for async consumption.
/// </summary>
/// <typeparam name="TRequest">The type of request to validate.</typeparam>
public abstract class RequestValidator<TRequest> : IRequestValidator<TRequest>, IRequestValidatorSync<TRequest>
    where TRequest : notnull
{
    /// <inheritdoc />
    public ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Validate(request));

    /// <inheritdoc />
    public abstract ValidationResult Validate(TRequest request);
}
