using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MediateX.Behaviors;

/// <summary>
/// Pipeline behavior that logs request execution with timing information.
/// Logs request start, completion (with duration), and failures.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly LoggingBehaviorOptions _options;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        LoggingBehaviorOptions? options = null)
    {
        _logger = logger;
        _options = options ?? LoggingBehaviorOptions.Default;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        if (_options.LogRequestStart)
        {
            Log.RequestStarting(_logger, requestName);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            if (_options.LogRequestFinish)
            {
                if (stopwatch.ElapsedMilliseconds >= _options.SlowRequestThresholdMs)
                {
                    Log.RequestCompletedSlow(_logger, requestName, stopwatch.ElapsedMilliseconds, _options.SlowRequestThresholdMs);
                }
                else
                {
                    Log.RequestCompleted(_logger, requestName, stopwatch.ElapsedMilliseconds);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (_options.LogRequestException)
            {
                Log.RequestFailed(_logger, requestName, stopwatch.ElapsedMilliseconds, ex);
            }

            throw;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Handling {RequestName}")]
        public static partial void RequestStarting(ILogger logger, string requestName);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Handled {RequestName} in {ElapsedMs}ms")]
        public static partial void RequestCompleted(ILogger logger, string requestName, long elapsedMs);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Handled {RequestName} in {ElapsedMs}ms (exceeded threshold of {ThresholdMs}ms)")]
        public static partial void RequestCompletedSlow(ILogger logger, string requestName, long elapsedMs, long thresholdMs);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Error,
            Message = "Request {RequestName} failed after {ElapsedMs}ms")]
        public static partial void RequestFailed(ILogger logger, string requestName, long elapsedMs, Exception exception);
    }
}

/// <summary>
/// Configuration options for <see cref="LoggingBehavior{TRequest, TResponse}"/>.
/// </summary>
public class LoggingBehaviorOptions
{
    /// <summary>
    /// Default options instance.
    /// </summary>
    public static LoggingBehaviorOptions Default { get; } = new();

    /// <summary>
    /// Whether to log when a request starts. Default is true.
    /// </summary>
    public bool LogRequestStart { get; set; } = true;

    /// <summary>
    /// Whether to log when a request completes successfully. Default is true.
    /// </summary>
    public bool LogRequestFinish { get; set; } = true;

    /// <summary>
    /// Whether to log when a request throws an exception. Default is true.
    /// </summary>
    public bool LogRequestException { get; set; } = true;

    /// <summary>
    /// Threshold in milliseconds for slow request warnings. Default is 500ms.
    /// Requests taking longer than this will be logged at Warning level.
    /// </summary>
    public long SlowRequestThresholdMs { get; set; } = 500;
}
