using System;
using System.Threading;
using System.Threading.Tasks;
using MediateX;
using MediateX.Behaviors;
using MediateX.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MediateX.Tests.Pipeline.Timeout;

public class TimeoutBehaviorTests
{
    #region Test Types

    public record FastCommand : IRequest<string>;

    public record SlowCommand(int DelayMs) : IRequest<string>;

    public record SlowResultCommand(int DelayMs) : IResultRequest<string>;

    public record CustomTimeoutCommand(int DelayMs) : IRequest<string>, IHasTimeout
    {
        public TimeSpan? Timeout => TimeSpan.FromMilliseconds(100);
    }

    public class FastHandler : IRequestHandler<FastCommand, string>
    {
        public Task<string> Handle(FastCommand request, CancellationToken cancellationToken)
            => Task.FromResult("Fast response");
    }

    public class SlowHandler : IRequestHandler<SlowCommand, string>
    {
        public async Task<string> Handle(SlowCommand request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            return "Slow response";
        }
    }

    public class SlowResultHandler : IRequestHandler<SlowResultCommand, Result<string>>
    {
        public async Task<Result<string>> Handle(SlowResultCommand request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            return Result<string>.Success("Slow response");
        }
    }

    public class CustomTimeoutHandler : IRequestHandler<CustomTimeoutCommand, string>
    {
        public async Task<string> Handle(CustomTimeoutCommand request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            return "Response";
        }
    }

    #endregion

    [Fact]
    public async Task Should_Complete_When_Request_Is_Fast()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<FastCommand>();
            cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(5));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new FastCommand());

        Assert.Equal("Fast response", result);
    }

    [Fact]
    public async Task Should_Throw_TimeoutException_When_Request_Exceeds_Timeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SlowCommand>();
            cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromMilliseconds(50));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<TimeoutException>(
            () => mediator.Send(new SlowCommand(500)));

        Assert.Contains("SlowCommand", exception.Message);
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public async Task Should_Use_Request_Specific_Timeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SlowCommand>();
            cfg.AddTimeoutBehavior(opt =>
            {
                opt.DefaultTimeout = TimeSpan.FromSeconds(30);
                opt.SetTimeout<SlowCommand>(TimeSpan.FromMilliseconds(50));
            });
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<TimeoutException>(
            () => mediator.Send(new SlowCommand(500)));
    }

    [Fact]
    public async Task Should_Use_IHasTimeout_Interface()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CustomTimeoutCommand>();
            cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(30));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // CustomTimeoutCommand has 100ms timeout via IHasTimeout
        await Assert.ThrowsAsync<TimeoutException>(
            () => mediator.Send(new CustomTimeoutCommand(500)));
    }

    [Fact]
    public async Task Should_Not_Timeout_When_Disabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SlowCommand>();
            cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = System.Threading.Timeout.InfiniteTimeSpan);
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new SlowCommand(100));

        Assert.Equal("Slow response", result);
    }

    [Fact]
    public void Options_Should_Have_Sensible_Defaults()
    {
        var options = new TimeoutBehaviorOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.DefaultTimeout);
        Assert.Empty(options.RequestTimeouts);
    }

    [Fact]
    public async Task Should_Apply_Custom_Options()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<FastCommand>();
            cfg.AddTimeoutBehavior(opt =>
            {
                opt.DefaultTimeout = TimeSpan.FromSeconds(60);
                opt.SetTimeout<SlowCommand>(TimeSpan.FromSeconds(10));
            });
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<TimeoutBehaviorOptions>();

        Assert.Equal(TimeSpan.FromSeconds(60), options.DefaultTimeout);
        Assert.Single(options.RequestTimeouts);
        Assert.Equal(TimeSpan.FromSeconds(10), options.RequestTimeouts[typeof(SlowCommand)]);
    }

    [Fact]
    public async Task Should_Register_Behavior_In_Pipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<FastCommand>();
            cfg.AddTimeoutBehavior();
        });

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<FastCommand, string>>();

        Assert.Contains(behaviors, b => b.GetType().GetGenericTypeDefinition() == typeof(TimeoutBehavior<,>));
    }

    #region TimeoutResultBehavior Tests

    [Fact]
    public async Task ResultBehavior_Should_Complete_When_Fast()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SlowResultCommand>();
            cfg.AddTimeoutResultBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(5));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new SlowResultCommand(10));

        Assert.True(result.IsSuccess);
        Assert.Equal("Slow response", result.Value);
    }

    [Fact]
    public async Task ResultBehavior_Should_Return_Failure_On_Timeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SlowResultCommand>();
            cfg.AddTimeoutResultBehavior(opt => opt.DefaultTimeout = TimeSpan.FromMilliseconds(50));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new SlowResultCommand(500));

        Assert.True(result.IsFailure);
        Assert.Equal("Timeout", result.Error.Code);
        Assert.Contains("SlowResultCommand", result.Error.Message);
    }

    #endregion
}
