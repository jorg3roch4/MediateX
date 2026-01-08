using System;
using System.Net.Http;
using MediateX.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MediateX.Behaviors;

/// <summary>
/// Pipeline behavior that retries failed requests with configurable retry logic.
/// Supports exponential backoff and exception filtering.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public partial class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly RetryBehaviorOptions _options;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>>? _logger;

    public RetryBehavior(
        RetryBehaviorOptions options,
        ILogger<RetryBehavior<TRequest, TResponse>>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var attempt = 0;
        var maxAttempts = _options.MaxRetryAttempts + 1; // +1 for initial attempt

        while (true)
        {
            attempt++;
            try
            {
                return await next(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxAttempts && ShouldRetry(ex))
            {
                var delay = CalculateDelay(attempt);

                if (_logger is not null)
                {
                    Log.RetryingRequest(_logger, requestName, attempt, _options.MaxRetryAttempts, delay.TotalMilliseconds, ex);
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool ShouldRetry(Exception exception)
    {
        if (_options.ShouldRetryException is not null)
        {
            return _options.ShouldRetryException(exception);
        }

        // Default: retry transient exceptions
        return exception is TimeoutException
            or TaskCanceledException { InnerException: TimeoutException }
            or HttpRequestException;
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        if (!_options.UseExponentialBackoff)
        {
            return _options.BaseDelay;
        }

        // Exponential backoff: baseDelay * 2^(attempt-1)
        var exponentialDelay = TimeSpan.FromMilliseconds(
            _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

        // Cap at max delay
        if (exponentialDelay > _options.MaxDelay)
        {
            exponentialDelay = _options.MaxDelay;
        }

        // Add jitter if enabled (up to 25% variation)
        if (_options.UseJitter)
        {
            var jitterFactor = 1.0 + (Random.Shared.NextDouble() - 0.5) * 0.5;
            exponentialDelay = TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitterFactor);
        }

        return exponentialDelay;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Warning,
            Message = "Retrying {RequestName} (attempt {Attempt}/{MaxAttempts}) after {DelayMs}ms")]
        public static partial void RetryingRequest(
            ILogger logger,
            string requestName,
            int attempt,
            int maxAttempts,
            double delayMs,
            Exception exception);
    }
}

/// <summary>
/// Pipeline behavior that retries requests returning <see cref="Result{T}"/> on failure.
/// Does not throw exceptions - returns failure result after all retries exhausted.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TValue">The value type in the Result.</typeparam>
public partial class RetryResultBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : notnull
{
    private readonly RetryBehaviorOptions _options;
    private readonly ILogger<RetryResultBehavior<TRequest, TValue>>? _logger;

    public RetryResultBehavior(
        RetryBehaviorOptions options,
        ILogger<RetryResultBehavior<TRequest, TValue>>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<Result<TValue>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue>> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var attempt = 0;
        var maxAttempts = _options.MaxRetryAttempts + 1;
        Result<TValue> lastResult = default!;

        while (attempt < maxAttempts)
        {
            attempt++;

            try
            {
                lastResult = await next(cancellationToken).ConfigureAwait(false);

                if (lastResult.IsSuccess)
                {
                    return lastResult;
                }

                // Check if we should retry this failure
                if (attempt < maxAttempts && ShouldRetryResult(lastResult))
                {
                    var delay = CalculateDelay(attempt);

                    if (_logger is not null)
                    {
                        Log.RetryingFailedResult(_logger, requestName, attempt, _options.MaxRetryAttempts, lastResult.Error.Code, delay.TotalMilliseconds);
                    }

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return lastResult;
            }
            catch (Exception ex) when (attempt < maxAttempts && ShouldRetryException(ex))
            {
                var delay = CalculateDelay(attempt);

                if (_logger is not null)
                {
                    Log.RetryingException(_logger, requestName, attempt, _options.MaxRetryAttempts, delay.TotalMilliseconds, ex);
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                lastResult = Result<TValue>.Failure("Exception", ex.Message);
            }
        }

        return lastResult;
    }

    private bool ShouldRetryResult(Result<TValue> result)
    {
        if (_options.ShouldRetryResultError is not null)
        {
            return _options.ShouldRetryResultError(result.Error);
        }

        // Default: retry transient error codes
        return result.Error.Code is "Timeout" or "Transient" or "ServiceUnavailable" or "TooManyRequests";
    }

    private bool ShouldRetryException(Exception exception)
    {
        if (_options.ShouldRetryException is not null)
        {
            return _options.ShouldRetryException(exception);
        }

        return exception is TimeoutException
            or TaskCanceledException { InnerException: TimeoutException }
            or HttpRequestException;
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        if (!_options.UseExponentialBackoff)
        {
            return _options.BaseDelay;
        }

        var exponentialDelay = TimeSpan.FromMilliseconds(
            _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

        if (exponentialDelay > _options.MaxDelay)
        {
            exponentialDelay = _options.MaxDelay;
        }

        if (_options.UseJitter)
        {
            var jitterFactor = 1.0 + (Random.Shared.NextDouble() - 0.5) * 0.5;
            exponentialDelay = TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitterFactor);
        }

        return exponentialDelay;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Warning,
            Message = "Retrying {RequestName} (attempt {Attempt}/{MaxAttempts}) due to error '{ErrorCode}' after {DelayMs}ms")]
        public static partial void RetryingFailedResult(
            ILogger logger,
            string requestName,
            int attempt,
            int maxAttempts,
            string errorCode,
            double delayMs);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Warning,
            Message = "Retrying {RequestName} (attempt {Attempt}/{MaxAttempts}) due to exception after {DelayMs}ms")]
        public static partial void RetryingException(
            ILogger logger,
            string requestName,
            int attempt,
            int maxAttempts,
            double delayMs,
            Exception exception);
    }
}

/// <summary>
/// Configuration options for <see cref="RetryBehavior{TRequest, TResponse}"/>.
/// </summary>
public class RetryBehaviorOptions
{
    /// <summary>
    /// Default options with 3 retries and exponential backoff.
    /// </summary>
    public static RetryBehaviorOptions Default { get; } = new();

    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries. Default is 200ms.
    /// With exponential backoff: 200ms, 400ms, 800ms, ...
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Maximum delay between retries (caps exponential growth). Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to use exponential backoff. Default is true.
    /// If false, uses constant delay (BaseDelay).
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Whether to add random jitter to delays (helps prevent thundering herd). Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Custom predicate to determine if an exception should trigger a retry.
    /// If null, uses default transient exception detection.
    /// </summary>
    public Func<Exception, bool>? ShouldRetryException { get; set; }

    /// <summary>
    /// Custom predicate to determine if a Result error should trigger a retry.
    /// Used by <see cref="RetryResultBehavior{TRequest, TValue}"/>.
    /// If null, retries errors with codes: Timeout, Transient, ServiceUnavailable, TooManyRequests.
    /// </summary>
    public Func<Error, bool>? ShouldRetryResultError { get; set; }
}
