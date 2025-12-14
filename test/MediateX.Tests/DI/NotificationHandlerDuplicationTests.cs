using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace MediateX.Tests.DI;

/// <summary>
/// Tests for issue #1118: Notification handler duplication when using inheritance.
/// When publishing a derived notification (E2), handlers for base type (E1)
/// should not be invoked multiple times due to contravariance registration issues.
/// </summary>
public class NotificationHandlerDuplicationTests
{
    // Track handler invocations
    private static readonly List<string> _invocations = [];

    // Base notification
    public class BaseNotification : INotification
    {
        public string? Message { get; set; }
    }

    // Derived notification
    public class DerivedNotification : BaseNotification
    {
    }

    // Handler for base notification (E1 -> C1)
    public class BaseNotificationHandler : INotificationHandler<BaseNotification>
    {
        public Task Handle(BaseNotification notification, CancellationToken cancellationToken)
        {
            _invocations.Add($"BaseHandler:{notification.GetType().Name}");
            return Task.CompletedTask;
        }
    }

    // Handler for derived notification (E2 -> C2)
    public class DerivedNotificationHandler : INotificationHandler<DerivedNotification>
    {
        public Task Handle(DerivedNotification notification, CancellationToken cancellationToken)
        {
            _invocations.Add($"DerivedHandler:{notification.GetType().Name}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Publishing_DerivedNotification_Should_Only_Invoke_DerivedHandler()
    {
        // Arrange
        _invocations.Clear();

        ServiceCollection services = new();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NotificationHandlerDuplicationTests>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act - Publish derived notification
        await mediator.Publish(new DerivedNotification { Message = "Test" });

        // Assert
        // With strict type matching, only handlers that explicitly declare
        // INotificationHandler<DerivedNotification> should be invoked.
        // BaseHandler (which handles BaseNotification) should NOT be invoked.
        var baseHandlerInvocations = _invocations.FindAll(x => x.StartsWith("BaseHandler:"));
        baseHandlerInvocations.Count.ShouldBe(0,
            $"BaseHandler should not be invoked for DerivedNotification. Invocations: [{string.Join(", ", _invocations)}]");

        // DerivedHandler should be invoked exactly once
        var derivedHandlerInvocations = _invocations.FindAll(x => x.StartsWith("DerivedHandler:"));
        derivedHandlerInvocations.Count.ShouldBe(1,
            $"DerivedHandler was invoked {derivedHandlerInvocations.Count} times. Invocations: [{string.Join(", ", _invocations)}]");
    }

    [Fact]
    public async Task Publishing_BaseNotification_Should_Only_Invoke_BaseHandler()
    {
        // Arrange
        _invocations.Clear();

        ServiceCollection services = new();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NotificationHandlerDuplicationTests>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act - Publish base notification
        await mediator.Publish(new BaseNotification { Message = "Test" });

        // Assert
        // Only BaseHandler should be invoked
        var baseHandlerInvocations = _invocations.FindAll(x => x.StartsWith("BaseHandler:"));
        baseHandlerInvocations.Count.ShouldBe(1,
            $"BaseHandler was invoked {baseHandlerInvocations.Count} times. Invocations: [{string.Join(", ", _invocations)}]");

        // DerivedHandler should NOT be invoked for base notification
        var derivedHandlerInvocations = _invocations.FindAll(x => x.StartsWith("DerivedHandler:"));
        derivedHandlerInvocations.Count.ShouldBe(0,
            $"DerivedHandler should not be invoked for BaseNotification. Invocations: [{string.Join(", ", _invocations)}]");
    }

    [Fact]
    public void ServiceCollection_Should_Not_Have_Duplicate_Handler_Registrations()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NotificationHandlerDuplicationTests>();
        });

        // Act - Check registrations for INotificationHandler<DerivedNotification>
        var derivedHandlerRegistrations = services
            .Where(sd => sd.ServiceType == typeof(INotificationHandler<DerivedNotification>))
            .ToList();

        // Assert
        // BaseNotificationHandler should NOT be registered for DerivedNotification
        var baseHandlerForDerived = derivedHandlerRegistrations
            .Where(sd => sd.ImplementationType == typeof(BaseNotificationHandler))
            .ToList();

        baseHandlerForDerived.Count.ShouldBe(0,
            $"BaseNotificationHandler should not be registered for INotificationHandler<DerivedNotification>. " +
            $"Found {baseHandlerForDerived.Count} registration(s). This causes duplicate invocations.");

        // DerivedNotificationHandler should be registered exactly once
        var derivedHandlerForDerived = derivedHandlerRegistrations
            .Where(sd => sd.ImplementationType == typeof(DerivedNotificationHandler))
            .ToList();

        derivedHandlerForDerived.Count.ShouldBe(1,
            $"DerivedNotificationHandler should be registered exactly once for INotificationHandler<DerivedNotification>");
    }
}
