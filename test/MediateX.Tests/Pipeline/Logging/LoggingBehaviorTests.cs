using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediateX;
using MediateX.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MediateX.Tests.Pipeline.Logging;

public class LoggingBehaviorTests
{
    #region Test Types

    public record LogTestCommand(string Value) : IRequest<string>;

    public record SlowCommand(int DelayMs) : IRequest<string>;

    public record FailingCommand(string Message) : IRequest<string>;

    public class LogTestHandler : IRequestHandler<LogTestCommand, string>
    {
        public Task<string> Handle(LogTestCommand request, CancellationToken cancellationToken)
            => Task.FromResult($"Handled: {request.Value}");
    }

    public class SlowHandler : IRequestHandler<SlowCommand, string>
    {
        public async Task<string> Handle(SlowCommand request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            return "Done";
        }
    }

    public class FailingHandler : IRequestHandler<FailingCommand, string>
    {
        public Task<string> Handle(FailingCommand request, CancellationToken cancellationToken)
            => throw new InvalidOperationException(request.Message);
    }

    #endregion

    #region Test Logger

    public class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
        }

        public record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
    }

    public class TestLoggerFactory : ILoggerFactory
    {
        private readonly Dictionary<string, object> _loggers = new();

        public TestLogger<T> GetTestLogger<T>()
        {
            var key = typeof(T).FullName!;
            if (!_loggers.TryGetValue(key, out var logger))
            {
                logger = new TestLogger<T>();
                _loggers[key] = logger;
            }
            return (TestLogger<T>)logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            if (!_loggers.TryGetValue(categoryName, out var logger))
            {
                var type = typeof(TestLogger<>).MakeGenericType(typeof(object));
                logger = Activator.CreateInstance(type)!;
                _loggers[categoryName] = logger;
            }
            return (ILogger)logger;
        }

        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    #endregion

    [Fact]
    public async Task Should_Log_Request_Start_And_Completion()
    {
        var loggerFactory = new TestLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(TestLogger<>));
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<LogTestCommand>();
            cfg.AddLoggingBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new LogTestCommand("Test"));

        Assert.Equal("Handled: Test", result);
    }

    [Fact]
    public async Task Should_Work_With_Default_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<LogTestCommand>();
            cfg.AddLoggingBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new LogTestCommand("Test"));

        Assert.Equal("Handled: Test", result);
    }

    [Fact]
    public async Task Should_Apply_Custom_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<LogTestCommand>();
            cfg.AddLoggingBehavior(options =>
            {
                options.LogRequestStart = false;
                options.LogRequestFinish = true;
                options.SlowRequestThresholdMs = 100;
            });
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<LoggingBehaviorOptions>();

        Assert.False(options.LogRequestStart);
        Assert.True(options.LogRequestFinish);
        Assert.Equal(100, options.SlowRequestThresholdMs);
    }

    [Fact]
    public async Task Should_Log_Exception_When_Handler_Fails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<FailingCommand>();
            cfg.AddLoggingBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new FailingCommand("Test error")));
    }

    [Fact]
    public async Task Should_Not_Interfere_With_Request_Processing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<LogTestCommand>();
            cfg.AddLoggingBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Execute multiple requests
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(mediator.Send(new LogTestCommand($"Test{i}")));
        }

        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"Handled: Test{i}", results[i]);
        }
    }

    [Fact]
    public void Options_Should_Have_Sensible_Defaults()
    {
        var options = new LoggingBehaviorOptions();

        Assert.True(options.LogRequestStart);
        Assert.True(options.LogRequestFinish);
        Assert.True(options.LogRequestException);
        Assert.Equal(500, options.SlowRequestThresholdMs);
    }

    [Fact]
    public void Options_Default_Instance_Should_Be_Immutable_Reference()
    {
        var default1 = LoggingBehaviorOptions.Default;
        var default2 = LoggingBehaviorOptions.Default;

        Assert.Same(default1, default2);
    }

    [Fact]
    public async Task Should_Register_Behavior_In_Pipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<LogTestCommand>();
            cfg.AddLoggingBehavior();
        });

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<LogTestCommand, string>>();

        Assert.Contains(behaviors, b => b.GetType().GetGenericTypeDefinition() == typeof(LoggingBehavior<,>));
    }
}
