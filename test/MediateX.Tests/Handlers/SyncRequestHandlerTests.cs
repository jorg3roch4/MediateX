using MediateX.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace MediateX.Tests.Handlers;

/// <summary>
/// Tests for ISyncRequestHandler and ISyncNotificationHandler interfaces.
/// These interfaces use ValueTask for optimized sync path performance.
/// </summary>
/// <remarks>
/// These tests use static state (Mediator.Freeze/Reset) and must not run in parallel.
/// </remarks>
[Collection("FreezeTests")]
public class SyncRequestHandlerTests : IDisposable
{
    public SyncRequestHandlerTests()
    {
        Mediator.Reset();
        CallTracker.Reset();
    }

    public void Dispose()
    {
        Mediator.Reset();
        CallTracker.Reset();
    }

    // Shared tracking for handler calls
    public static class CallTracker
    {
        public static List<string> Calls { get; } = [];
        public static void Reset() => Calls.Clear();
    }

    #region Test Types

    public record SyncPing(string Message) : IRequest<SyncPong>;
    public record SyncPong(string Message);

    public record SyncVoidPing : IRequest;

    public record SyncNotification(string Message) : INotification;

    public record DualPing(string Message) : IRequest<string>;

    // Standard IRequestHandler using Task
    public record TaskPing(string Message) : IRequest<TaskPong>;
    public record TaskPong(string Message);

    #endregion

    #region Handlers

    public class SyncPingHandler : ISyncRequestHandler<SyncPing, SyncPong>
    {
        public ValueTask<SyncPong> Handle(SyncPing request, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add("SyncPingHandler");
            // Synchronous completion - zero allocation path
            return new ValueTask<SyncPong>(new SyncPong(request.Message + " SyncPong"));
        }
    }

    public class SyncVoidPingHandler : ISyncRequestHandler<SyncVoidPing>
    {
        public ValueTask Handle(SyncVoidPing request, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add("SyncVoidPingHandler");
            return ValueTask.CompletedTask;
        }
    }

    public class SyncNotificationHandlerImpl : ISyncNotificationHandler<SyncNotification>
    {
        public ValueTask Handle(SyncNotification notification, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add("SyncNotificationHandler:" + notification.Message);
            return ValueTask.CompletedTask;
        }
    }

    // Handler that implements both interfaces (ISyncRequestHandler has priority)
    public class DualHandler : ISyncRequestHandler<DualPing, string>, IRequestHandler<DualPing, string>
    {
        public ValueTask<string> Handle(DualPing request, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add("DualHandler:ISyncRequestHandler");
            return new ValueTask<string>(request.Message + " via Sync");
        }

        Task<string> IRequestHandler<DualPing, string>.Handle(DualPing request, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add("DualHandler:IRequestHandler");
            return Task.FromResult(request.Message + " via Async");
        }
    }

    // Standard Task-based handler for comparison
    public class TaskPingHandler : IRequestHandler<TaskPing, TaskPong>
    {
        public Task<TaskPong> Handle(TaskPing request, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add("TaskPingHandler");
            return Task.FromResult(new TaskPong(request.Message + " TaskPong"));
        }
    }

    // Pipeline behavior for testing with sync handlers
    public class TestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            CallTracker.Calls.Add($"TestBehavior:Before:{typeof(TRequest).Name}");
            var response = await next(cancellationToken);
            CallTracker.Calls.Add($"TestBehavior:After:{typeof(TRequest).Name}");
            return response;
        }
    }

    #endregion

    #region Basic Resolution Tests

    [Fact]
    public async Task ISyncRequestHandler_Should_Be_Resolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new SyncPing("Test"), TestContext.Current.CancellationToken);

        // Assert
        response.Message.ShouldBe("Test SyncPong");
        CallTracker.Calls.ShouldContain("SyncPingHandler");
    }

    [Fact]
    public async Task ISyncRequestHandler_Void_Should_Be_Resolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new SyncVoidPing(), TestContext.Current.CancellationToken);

        // Assert
        CallTracker.Calls.ShouldContain("SyncVoidPingHandler");
    }

    [Fact]
    public async Task ISyncRequestHandler_Has_Priority_Over_IRequestHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new DualPing("Test"), TestContext.Current.CancellationToken);

        // Assert
        response.ShouldBe("Test via Sync");
        CallTracker.Calls.ShouldContain("DualHandler:ISyncRequestHandler");
        CallTracker.Calls.ShouldNotContain("DualHandler:IRequestHandler");
    }

    [Fact]
    public async Task IRequestHandler_Still_Works_When_No_ISyncRequestHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new TaskPing("Test"), TestContext.Current.CancellationToken);

        // Assert
        response.Message.ShouldBe("Test TaskPong");
        CallTracker.Calls.ShouldContain("TaskPingHandler");
    }

    [Fact]
    public async Task ISyncNotificationHandler_Should_Be_Resolved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Publish(new SyncNotification("Hello"), TestContext.Current.CancellationToken);

        // Assert
        CallTracker.Calls.ShouldContain("SyncNotificationHandler:Hello");
    }

    #endregion

    #region Pipeline Behavior Tests

    [Fact]
    public async Task ISyncRequestHandler_Works_With_Behaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>();
            cfg.AddOpenBehavior(typeof(TestBehavior<,>));
        });
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new SyncPing("Test"), TestContext.Current.CancellationToken);

        // Assert
        response.Message.ShouldBe("Test SyncPong");
        CallTracker.Calls.Count.ShouldBe(3);
        CallTracker.Calls[0].ShouldBe("TestBehavior:Before:SyncPing");
        CallTracker.Calls[1].ShouldBe("SyncPingHandler");
        CallTracker.Calls[2].ShouldBe("TestBehavior:After:SyncPing");
    }

    [Fact]
    public async Task ISyncRequestHandler_Void_Works_With_Behaviors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>();
            cfg.AddOpenBehavior(typeof(TestBehavior<,>));
        });
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new SyncVoidPing(), TestContext.Current.CancellationToken);

        // Assert
        CallTracker.Calls.Count.ShouldBe(3);
        CallTracker.Calls[0].ShouldBe("TestBehavior:Before:SyncVoidPing");
        CallTracker.Calls[1].ShouldBe("SyncVoidPingHandler");
        CallTracker.Calls[2].ShouldBe("TestBehavior:After:SyncVoidPing");
    }

    #endregion

    #region Freeze Tests

    [Fact]
    public async Task ISyncRequestHandler_Works_With_Freeze()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Warmup
        await mediator.Send(new SyncPing("Warmup"), TestContext.Current.CancellationToken);
        CallTracker.Reset();

        // Freeze
        Mediator.Freeze();
        Mediator.IsFrozen.ShouldBeTrue();

        // Act - use frozen cache
        var response = await mediator.Send(new SyncPing("Frozen"), TestContext.Current.CancellationToken);

        // Assert
        response.Message.ShouldBe("Frozen SyncPong");
        CallTracker.Calls.ShouldContain("SyncPingHandler");
    }

    [Fact]
    public async Task ISyncRequestHandler_Void_Works_With_Freeze()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Warmup
        await mediator.Send(new SyncVoidPing(), TestContext.Current.CancellationToken);
        CallTracker.Reset();

        // Freeze
        Mediator.Freeze();

        // Act
        await mediator.Send(new SyncVoidPing(), TestContext.Current.CancellationToken);

        // Assert
        CallTracker.Calls.ShouldContain("SyncVoidPingHandler");
    }

    #endregion

    #region Dynamic Dispatch Tests

    [Fact]
    public async Task ISyncRequestHandler_Works_With_Dynamic_Dispatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        object request = new SyncPing("Dynamic");
        var response = await mediator.Send(request, TestContext.Current.CancellationToken);

        // Assert
        var pong = response.ShouldBeOfType<SyncPong>();
        pong.Message.ShouldBe("Dynamic SyncPong");
        CallTracker.Calls.ShouldContain("SyncPingHandler");
    }

    [Fact]
    public async Task ISyncRequestHandler_Void_Works_With_Dynamic_Dispatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        object request = new SyncVoidPing();
        var response = await mediator.Send(request, TestContext.Current.CancellationToken);

        // Assert
        response.ShouldBeOfType<Unit>();
        CallTracker.Calls.ShouldContain("SyncVoidPingHandler");
    }

    #endregion

    #region ValueTask Sync Completion Tests

    [Fact]
    public async Task ISyncRequestHandler_Returns_Completed_ValueTask_Synchronously()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<SyncRequestHandlerTests>());
        var provider = services.BuildServiceProvider();

        // Get the handler directly to test ValueTask behavior
        var handler = provider.GetRequiredService<ISyncRequestHandler<SyncPing, SyncPong>>();

        // Act
        var valueTask = handler.Handle(new SyncPing("Test"), CancellationToken.None);

        // Assert - ValueTask should complete synchronously
        valueTask.IsCompletedSuccessfully.ShouldBeTrue();
        var result = await valueTask;
        result.Message.ShouldBe("Test SyncPong");
    }

    #endregion
}
