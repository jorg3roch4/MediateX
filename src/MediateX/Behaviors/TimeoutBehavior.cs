using System;
using System.Collections.Generic;
using MediateX.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MediateX.Behaviors;

/// <summary>
/// Pipeline behavior that enforces a timeout on request execution.
/// Throws <see cref="TimeoutException"/> if the request exceeds the configured timeout.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public partial class TimeoutBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly TimeoutBehaviorOptions _options;
    private readonly ILogger<TimeoutBehavior<TRequest, TResponse>>? _logger;

    public TimeoutBehavior(
        TimeoutBehaviorOptions options,
        ILogger<TimeoutBehavior<TRequest, TResponse>>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timeout = GetTimeout(request);

        if (timeout == Timeout.InfiniteTimeSpan || timeout == TimeSpan.Zero)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await next(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var requestName = typeof(TRequest).Name;

            if (_logger is not null)
            {
                Log.RequestTimedOut(_logger, requestName, timeout.TotalMilliseconds);
            }

            throw new TimeoutException(
                $"Request '{requestName}' timed out after {timeout.TotalMilliseconds}ms");
        }
    }

    private TimeSpan GetTimeout(TRequest request)
    {
        // Check for custom timeout via attribute or interface
        if (request is IHasTimeout hasTimeout && hasTimeout.Timeout.HasValue)
        {
            return hasTimeout.Timeout.Value;
        }

        // Check for request-specific timeout in options
        if (_options.RequestTimeouts.TryGetValue(typeof(TRequest), out var specificTimeout))
        {
            return specificTimeout;
        }

        return _options.DefaultTimeout;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 20,
            Level = LogLevel.Warning,
            Message = "Request {RequestName} timed out after {TimeoutMs}ms")]
        public static partial void RequestTimedOut(ILogger logger, string requestName, double timeoutMs);
    }
}

/// <summary>
/// Pipeline behavior that enforces a timeout and returns <see cref="Result{T}"/> on timeout.
/// Does not throw exceptions - returns a failure result instead.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TValue">The value type in the Result.</typeparam>
public partial class TimeoutResultBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : notnull
{
    private readonly TimeoutBehaviorOptions _options;
    private readonly ILogger<TimeoutResultBehavior<TRequest, TValue>>? _logger;

    public TimeoutResultBehavior(
        TimeoutBehaviorOptions options,
        ILogger<TimeoutResultBehavior<TRequest, TValue>>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<Result<TValue>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue>> next,
        CancellationToken cancellationToken)
    {
        var timeout = GetTimeout(request);

        if (timeout == Timeout.InfiniteTimeSpan || timeout == TimeSpan.Zero)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await next(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var requestName = typeof(TRequest).Name;

            if (_logger is not null)
            {
                Log.RequestTimedOut(_logger, requestName, timeout.TotalMilliseconds);
            }

            return Result<TValue>.Failure(
                "Timeout",
                $"Request '{requestName}' timed out after {timeout.TotalMilliseconds}ms");
        }
    }

    private TimeSpan GetTimeout(TRequest request)
    {
        if (request is IHasTimeout hasTimeout && hasTimeout.Timeout.HasValue)
        {
            return hasTimeout.Timeout.Value;
        }

        if (_options.RequestTimeouts.TryGetValue(typeof(TRequest), out var specificTimeout))
        {
            return specificTimeout;
        }

        return _options.DefaultTimeout;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 21,
            Level = LogLevel.Warning,
            Message = "Request {RequestName} timed out after {TimeoutMs}ms")]
        public static partial void RequestTimedOut(ILogger logger, string requestName, double timeoutMs);
    }
}

/// <summary>
/// Interface for requests that specify their own timeout.
/// </summary>
public interface IHasTimeout
{
    /// <summary>
    /// The timeout for this request. If null, uses the default timeout from options.
    /// </summary>
    TimeSpan? Timeout { get; }
}

/// <summary>
/// Configuration options for <see cref="TimeoutBehavior{TRequest, TResponse}"/>.
/// </summary>
public class TimeoutBehaviorOptions
{
    /// <summary>
    /// Default options with 30 second timeout.
    /// </summary>
    public static TimeoutBehaviorOptions Default { get; } = new();

    /// <summary>
    /// Default timeout for all requests. Default is 30 seconds.
    /// Set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable timeout.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Request-specific timeouts. These override the default timeout.
    /// </summary>
    public Dictionary<Type, TimeSpan> RequestTimeouts { get; } = new();

    /// <summary>
    /// Sets a specific timeout for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="timeout">The timeout for this request type.</param>
    /// <returns>This options instance for chaining.</returns>
    public TimeoutBehaviorOptions SetTimeout<TRequest>(TimeSpan timeout)
    {
        RequestTimeouts[typeof(TRequest)] = timeout;
        return this;
    }
}
