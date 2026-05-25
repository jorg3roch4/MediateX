using MediateX.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace MediateX.Tests.Core;

/// <summary>
/// Tests for the Mediator.Freeze() functionality which converts handler caches
/// from ConcurrentDictionary to FrozenDictionary for improved lookup performance.
/// </summary>
/// <remarks>
/// These tests use static state (Mediator.Freeze/Reset) and must not run in parallel
/// with other tests that may affect that state.
/// </remarks>
[Collection("FreezeTests")]
public class FreezeTests : IDisposable
{
    public FreezeTests()
    {
        // Reset mediator state before each test
        Mediator.Reset();
    }

    public void Dispose()
    {
        // Reset mediator state after each test
        Mediator.Reset();
    }

    public record Ping(string Message) : IRequest<Pong>;
    public record Pong(string Message);
    public record VoidPing : IRequest;
    public record TestNotification(string Message) : INotification;

    public class PingHandler : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(new Pong(request.Message + " Pong"));
    }

    public class VoidPingHandler : IRequestHandler<VoidPing>
    {
        public Task Handle(VoidPing request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public static int CallCount { get; set; }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private IMediator CreateMediator()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<FreezeTests>();
        });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMediator>();
    }

    [Fact]
    public void IsFrozen_Should_Be_False_By_Default()
    {
        Mediator.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public void Freeze_Should_Set_IsFrozen_To_True()
    {
        // Arrange
        var mediator = CreateMediator();

        // Act
        Mediator.Freeze();

        // Assert
        Mediator.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public void Freeze_Should_Be_Idempotent()
    {
        // Arrange
        var mediator = CreateMediator();

        // Act - call Freeze multiple times
        Mediator.Freeze();
        Mediator.Freeze();
        Mediator.Freeze();

        // Assert - should not throw and should remain frozen
        Mediator.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_Should_Work_Before_Freeze()
    {
        // Arrange
        var mediator = CreateMediator();

        // Act
        var response = await mediator.Send(new Ping("Test"), TestContext.Current.CancellationToken);

        // Assert
        response.Message.ShouldBe("Test Pong");
        Mediator.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public async Task Send_Should_Work_After_Freeze()
    {
        // Arrange
        var mediator = CreateMediator();

        // Warmup - resolve handler before freeze
        await mediator.Send(new Ping("Warmup"), TestContext.Current.CancellationToken);

        // Act
        Mediator.Freeze();
        var response = await mediator.Send(new Ping("Test"), TestContext.Current.CancellationToken);

        // Assert
        response.Message.ShouldBe("Test Pong");
        Mediator.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_Void_Should_Work_After_Freeze()
    {
        // Arrange
        var mediator = CreateMediator();

        // Warmup
        await mediator.Send(new VoidPing(), TestContext.Current.CancellationToken);

        // Act
        Mediator.Freeze();
        await mediator.Send(new VoidPing(), TestContext.Current.CancellationToken);

        // Assert - should complete without exception
        Mediator.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public async Task Publish_Should_Work_After_Freeze()
    {
        // Arrange
        var mediator = CreateMediator();
        TestNotificationHandler.CallCount = 0;

        // Warmup
        await mediator.Publish(new TestNotification("Warmup"), TestContext.Current.CancellationToken);

        // Act
        Mediator.Freeze();
        await mediator.Publish(new TestNotification("Test"), TestContext.Current.CancellationToken);

        // Assert
        TestNotificationHandler.CallCount.ShouldBe(2);
        Mediator.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    public async Task New_Handler_Types_Should_Work_After_Freeze()
    {
        // Arrange
        var mediator = CreateMediator();

        // Freeze without warming up any handlers
        Mediator.Freeze();

        // Act - first time resolving this handler type after freeze
        var response = await mediator.Send(new Ping("Test"), TestContext.Current.CancellationToken);

        // Assert - should fall back to warmup cache for new types
        response.Message.ShouldBe("Test Pong");
    }

    [Fact]
    public async Task Dynamic_Send_Should_Work_After_Freeze()
    {
        // Arrange
        var mediator = CreateMediator();

        // Warmup with dynamic dispatch
        object request = new Ping("Warmup");
        await mediator.Send(request, TestContext.Current.CancellationToken);

        // Act
        Mediator.Freeze();
        request = new Ping("Test");
        var response = await mediator.Send(request, TestContext.Current.CancellationToken);

        // Assert
        response.ShouldNotBeNull();
        ((Pong)response!).Message.ShouldBe("Test Pong");
    }

    [Fact]
    public void Reset_Should_Clear_Frozen_State()
    {
        // Arrange
        var mediator = CreateMediator();
        Mediator.Freeze();
        Mediator.IsFrozen.ShouldBeTrue();

        // Act
        Mediator.Reset();

        // Assert
        Mediator.IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public async Task Multiple_Requests_Should_Use_Frozen_Cache()
    {
        // Arrange
        var mediator = CreateMediator();

        // Warmup
        await mediator.Send(new Ping("Warmup"), TestContext.Current.CancellationToken);

        // Freeze
        Mediator.Freeze();

        // Act - multiple requests should all use frozen cache
        for (int i = 0; i < 100; i++)
        {
            var response = await mediator.Send(new Ping($"Request {i}"), TestContext.Current.CancellationToken);
            response.Message.ShouldBe($"Request {i} Pong");
        }

        // Assert
        Mediator.IsFrozen.ShouldBeTrue();
    }
}
