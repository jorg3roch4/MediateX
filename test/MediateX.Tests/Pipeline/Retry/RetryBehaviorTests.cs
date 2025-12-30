using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediateX;
using MediateX.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MediateX.Tests.Pipeline.Retry;

public class RetryBehaviorTests
{
    #region Test Types

    // Shared state to track attempts across handler instances
    public class AttemptTracker
    {
        public int Attempts { get; set; }
    }

    public record RetryTestCommand(int FailCount, AttemptTracker Tracker) : IRequest<string>;

    public record RetryResultCommand(int FailCount, AttemptTracker Tracker) : IResultRequest<string>;

    public class FailingHandler : IRequestHandler<RetryTestCommand, string>
    {
        public Task<string> Handle(RetryTestCommand request, CancellationToken cancellationToken)
        {
            request.Tracker.Attempts++;
            if (request.Tracker.Attempts <= request.FailCount)
            {
                throw new TimeoutException($"Attempt {request.Tracker.Attempts} failed");
            }
            return Task.FromResult($"Success on attempt {request.Tracker.Attempts}");
        }
    }

    public class FailingResultHandler : IRequestHandler<RetryResultCommand, Result<string>>
    {
        public Task<Result<string>> Handle(RetryResultCommand request, CancellationToken cancellationToken)
        {
            request.Tracker.Attempts++;
            if (request.Tracker.Attempts <= request.FailCount)
            {
                return Task.FromResult(Result<string>.Failure("Transient", $"Attempt {request.Tracker.Attempts} failed"));
            }
            return Task.FromResult(Result<string>.Success($"Success on attempt {request.Tracker.Attempts}"));
        }
    }

    public record AlwaysFailsCommand : IRequest<string>;

    public class AlwaysFailsHandler : IRequestHandler<AlwaysFailsCommand, string>
    {
        public Task<string> Handle(AlwaysFailsCommand request, CancellationToken cancellationToken)
            => throw new TimeoutException("Always fails");
    }

    public record CustomExceptionCommand(int FailCount, AttemptTracker Tracker) : IRequest<string>;

    public class CustomException : Exception
    {
        public CustomException(string message) : base(message) { }
    }

    public class CustomExceptionHandler : IRequestHandler<CustomExceptionCommand, string>
    {
        public Task<string> Handle(CustomExceptionCommand request, CancellationToken cancellationToken)
        {
            request.Tracker.Attempts++;
            if (request.Tracker.Attempts <= request.FailCount)
            {
                throw new CustomException($"Attempt {request.Tracker.Attempts}");
            }
            return Task.FromResult($"Success on attempt {request.Tracker.Attempts}");
        }
    }

    #endregion

    [Fact]
    public async Task Should_Succeed_Without_Retry_When_No_Failure()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryTestCommand>();
            cfg.AddRetryBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        var result = await mediator.Send(new RetryTestCommand(0, tracker));

        Assert.Equal("Success on attempt 1", result);
        Assert.Equal(1, tracker.Attempts);
    }

    [Fact]
    public async Task Should_Retry_On_Transient_Exception()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryTestCommand>();
            cfg.AddRetryBehavior(opt =>
            {
                opt.BaseDelay = TimeSpan.FromMilliseconds(10);
                opt.UseExponentialBackoff = false;
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        var result = await mediator.Send(new RetryTestCommand(2, tracker));

        Assert.Equal("Success on attempt 3", result);
        Assert.Equal(3, tracker.Attempts);
    }

    [Fact]
    public async Task Should_Throw_After_Max_Retries_Exhausted()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<AlwaysFailsCommand>();
            cfg.AddRetryBehavior(opt =>
            {
                opt.MaxRetryAttempts = 2;
                opt.BaseDelay = TimeSpan.FromMilliseconds(10);
                opt.UseExponentialBackoff = false;
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<TimeoutException>(
            () => mediator.Send(new AlwaysFailsCommand()));
    }

    [Fact]
    public async Task Should_Use_Custom_Exception_Filter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CustomExceptionCommand>();
            cfg.AddRetryBehavior(opt =>
            {
                opt.BaseDelay = TimeSpan.FromMilliseconds(10);
                opt.UseExponentialBackoff = false;
                opt.ShouldRetryException = ex => ex is CustomException;
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        var result = await mediator.Send(new CustomExceptionCommand(2, tracker));

        Assert.Equal("Success on attempt 3", result);
        Assert.Equal(3, tracker.Attempts);
    }

    [Fact]
    public async Task Should_Not_Retry_Non_Transient_Exception_By_Default()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CustomExceptionCommand>();
            cfg.AddRetryBehavior(opt =>
            {
                opt.BaseDelay = TimeSpan.FromMilliseconds(10);
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        // CustomException is not in the default retry list
        await Assert.ThrowsAsync<CustomException>(
            () => mediator.Send(new CustomExceptionCommand(1, tracker)));
    }

    [Fact]
    public void Options_Should_Have_Sensible_Defaults()
    {
        var options = new RetryBehaviorOptions();

        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.BaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), options.MaxDelay);
        Assert.True(options.UseExponentialBackoff);
        Assert.True(options.UseJitter);
        Assert.Null(options.ShouldRetryException);
        Assert.Null(options.ShouldRetryResultError);
    }

    [Fact]
    public async Task Should_Apply_Custom_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryTestCommand>();
            cfg.AddRetryBehavior(opt =>
            {
                opt.MaxRetryAttempts = 5;
                opt.BaseDelay = TimeSpan.FromMilliseconds(100);
                opt.UseExponentialBackoff = false;
                opt.UseJitter = false;
            });
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RetryBehaviorOptions>();

        Assert.Equal(5, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.BaseDelay);
        Assert.False(options.UseExponentialBackoff);
        Assert.False(options.UseJitter);
    }

    [Fact]
    public async Task Should_Register_Behavior_In_Pipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryTestCommand>();
            cfg.AddRetryBehavior();
        });

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<RetryTestCommand, string>>();

        Assert.Contains(behaviors, b => b.GetType().GetGenericTypeDefinition() == typeof(RetryBehavior<,>));
    }

    #region RetryResultBehavior Tests

    [Fact]
    public async Task ResultBehavior_Should_Succeed_On_First_Attempt()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryResultCommand>();
            cfg.AddRetryResultBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        var result = await mediator.Send(new RetryResultCommand(0, tracker));

        Assert.True(result.IsSuccess);
        Assert.Equal("Success on attempt 1", result.Value);
        Assert.Equal(1, tracker.Attempts);
    }

    [Fact]
    public async Task ResultBehavior_Should_Retry_On_Transient_Error()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryResultCommand>();
            cfg.AddRetryResultBehavior(opt =>
            {
                opt.BaseDelay = TimeSpan.FromMilliseconds(10);
                opt.UseExponentialBackoff = false;
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        var result = await mediator.Send(new RetryResultCommand(2, tracker));

        Assert.True(result.IsSuccess);
        Assert.Equal("Success on attempt 3", result.Value);
        Assert.Equal(3, tracker.Attempts);
    }

    [Fact]
    public async Task ResultBehavior_Should_Return_Failure_After_Max_Retries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RetryResultCommand>();
            cfg.AddRetryResultBehavior(opt =>
            {
                opt.MaxRetryAttempts = 2;
                opt.BaseDelay = TimeSpan.FromMilliseconds(10);
                opt.UseExponentialBackoff = false;
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var tracker = new AttemptTracker();

        var result = await mediator.Send(new RetryResultCommand(10, tracker)); // More failures than retries

        Assert.True(result.IsFailure);
        Assert.Equal("Transient", result.Error.Code);
    }

    #endregion
}
