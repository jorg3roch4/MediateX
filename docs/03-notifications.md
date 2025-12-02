# Notifications & Events

Notifications in MediateX enable the publish/subscribe (pub/sub) pattern, allowing you to broadcast events to multiple handlers. Unlike requests which have a single handler, notifications can be handled by zero or more handlers.

---

## Core Concepts

### INotification Interface

The `INotification` interface is a marker interface that identifies a class as a notification:

```csharp
public interface INotification { }
```

Any class implementing this interface can be published through the mediator.

**Example:**

```csharp
public record OrderPlacedNotification(int OrderId, decimal Total, string CustomerEmail) : INotification;

public record UserRegisteredNotification(int UserId, string Email) : INotification;

public record PaymentFailedNotification(int OrderId, string Reason) : INotification;
```

### INotificationHandler Interface

Handlers implement `INotificationHandler<TNotification>` to process notifications:

```csharp
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
```

**Key characteristics:**
- Multiple handlers can handle the same notification type
- Handlers execute based on the configured publishing strategy
- Each handler is independent and isolated
- Handlers should not return values or modify the notification

---

## Creating Notification Handlers

### Async Handlers

Most handlers should be async:

```csharp
public class SendOrderConfirmationEmail : INotificationHandler<OrderPlacedNotification>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendOrderConfirmationEmail> _logger;

    public SendOrderConfirmationEmail(
        IEmailService emailService,
        ILogger<SendOrderConfirmationEmail> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending order confirmation email for order {OrderId}", notification.OrderId);

        await _emailService.SendOrderConfirmationAsync(
            notification.CustomerEmail,
            notification.OrderId,
            notification.Total,
            cancellationToken);
    }
}
```

### Synchronous Handlers

Use the `NotificationHandler<TNotification>` base class for synchronous operations:

```csharp
public class LogOrderPlaced : NotificationHandler<OrderPlacedNotification>
{
    private readonly ILogger<LogOrderPlaced> _logger;

    public LogOrderPlaced(ILogger<LogOrderPlaced> logger) => _logger = logger;

    protected override void Handle(OrderPlacedNotification notification)
    {
        _logger.LogInformation(
            "Order {OrderId} placed for {Total:C}",
            notification.OrderId,
            notification.Total);
    }
}
```

The base class automatically wraps your synchronous code in a completed `Task`.

---

## Multiple Handlers Example

One of the most powerful features of notifications is the ability to have multiple handlers:

```csharp
// The notification
public record OrderPlacedNotification(
    int OrderId,
    decimal Total,
    string CustomerEmail,
    List<int> ProductIds) : INotification;

// Handler 1: Send confirmation email
public class EmailConfirmationHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly IEmailService _emailService;

    public EmailConfirmationHandler(IEmailService emailService)
        => _emailService = emailService;

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
        => _emailService.SendConfirmationAsync(notification.CustomerEmail, notification.OrderId, cancellationToken);
}

// Handler 2: Update inventory
public class UpdateInventoryHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly IInventoryService _inventoryService;

    public UpdateInventoryHandler(IInventoryService inventoryService)
        => _inventoryService = inventoryService;

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
        => _inventoryService.DecrementStockAsync(notification.ProductIds, cancellationToken);
}

// Handler 3: Log for analytics
public class AnalyticsHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsHandler(IAnalyticsService analyticsService)
        => _analyticsService = analyticsService;

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
        => _analyticsService.TrackOrderAsync(notification.OrderId, notification.Total, cancellationToken);
}

// Handler 4: Award loyalty points
public class LoyaltyPointsHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly ILoyaltyService _loyaltyService;

    public LoyaltyPointsHandler(ILoyaltyService loyaltyService)
        => _loyaltyService = loyaltyService;

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        var points = (int)(notification.Total * 10); // 10 points per dollar
        return _loyaltyService.AwardPointsAsync(notification.CustomerEmail, points, cancellationToken);
    }
}

// Usage - all four handlers will be invoked
await _mediator.Publish(new OrderPlacedNotification(
    orderId: 123,
    total: 99.99m,
    customerEmail: "customer@example.com",
    productIds: new List<int> { 1, 2, 3 }
));
```

---

## Publishing Notifications

Use `IPublisher` or `IMediator` to publish notifications:

```csharp
public class OrderService
{
    private readonly IPublisher _publisher;

    public OrderService(IPublisher publisher) => _publisher = publisher;

    public async Task<int> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // Create the order
        var orderId = await SaveOrderToDatabase(request);

        // Publish notification - all handlers will be invoked
        await _publisher.Publish(
            new OrderPlacedNotification(orderId, request.Total, request.CustomerEmail),
            cancellationToken);

        return orderId;
    }
}
```

---

## Publishing Strategies

MediateX provides two built-in strategies for publishing notifications to multiple handlers:

### ForeachAwaitPublisher (Default)

Executes handlers **sequentially**, awaiting each one before starting the next:

```csharp
foreach (var handler in handlers)
{
    await handler(notification, cancellationToken);
}
```

**Characteristics:**
- Handlers execute in order
- Each handler completes before the next starts
- If a handler throws, subsequent handlers won't execute
- Predictable execution order
- Lower memory usage

**When to use:**
- When handlers have dependencies on each other
- When you need predictable ordering
- When handlers share resources that shouldn't be accessed concurrently

### TaskWhenAllPublisher

Executes handlers **in parallel**, waiting for all to complete:

```csharp
var tasks = handlers
    .Select(handler => handler(notification, cancellationToken))
    .ToArray();

return Task.WhenAll(tasks);
```

**Characteristics:**
- All handlers start immediately
- Waits for all handlers to complete
- If any handler throws, you'll get an `AggregateException`
- Faster total execution time
- Higher memory usage

**When to use:**
- When handlers are independent
- When you want maximum performance
- When handlers can safely run concurrently

### Configuring the Publisher

Set the publisher during registration:

```csharp
using MediateX.Publishing;

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Option 1: Use built-in publisher instance
    cfg.NotificationPublisher = new TaskWhenAllPublisher();

    // Option 2: Use built-in publisher by type (will be resolved from DI)
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
});
```

---

## Custom Notification Publishers

Create a custom publisher by implementing `INotificationPublisher`:

```csharp
public interface INotificationPublisher
{
    Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken);
}
```

### Example: Stop On First Exception

```csharp
using MediateX.Core;

public class StopOnExceptionPublisher : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            // Will stop at first exception
            await handler.HandlerCallback(notification, cancellationToken);
        }
    }
}
```

### Example: Continue On Exception

```csharp
using MediateX.Core;

public class ContinueOnExceptionPublisher : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        List<Exception> exceptions = [];

        foreach (var handler in handlerExecutors)
        {
            try
            {
                await handler.HandlerCallback(notification, cancellationToken);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException(exceptions);
        }
    }
}
```

### Example: Parallel With Timeout

```csharp
using MediateX.Core;

public class ParallelWithTimeoutPublisher : INotificationPublisher
{
    private readonly TimeSpan _timeout;

    public ParallelWithTimeoutPublisher(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var tasks = handlerExecutors
            .Select(handler => handler.HandlerCallback(notification, linkedCts.Token))
            .ToArray();

        await Task.WhenAll(tasks);
    }
}
```

### Registering Custom Publishers

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new ContinueOnExceptionPublisher();
});
```

---

## Best Practices

### Design Guidelines

1. **Keep handlers independent:** Each handler should work independently without relying on other handlers
2. **Use descriptive names:** Name notifications as past-tense events (e.g., `OrderPlaced`, `UserRegistered`)
3. **Include relevant data:** Put all necessary data in the notification to avoid additional queries
4. **Make notifications immutable:** Use records or readonly properties
5. **Handle exceptions:** Decide whether handlers should fail independently or together

### Performance Considerations

1. **Choose the right publisher:**
   - Use `ForeachAwaitPublisher` for ordered execution or shared resources
   - Use `TaskWhenAllPublisher` for independent, parallel operations

2. **Avoid heavy operations:** If a handler does expensive work, consider using a background job queue

3. **Be mindful of transaction scope:** If publishing within a transaction, ensure handlers don't cause deadlocks

### Error Handling

1. **Handler isolation:** Design handlers to fail independently when using parallel execution
2. **Logging:** Always log in notification handlers to track execution
3. **Idempotency:** Make handlers idempotent if possible, in case they're retried
4. **Graceful degradation:** Non-critical handlers should not prevent critical operations from completing

### Common Patterns

**Domain Events:**

```csharp
// Publish domain events after persisting changes
public class OrderService
{
    private readonly IOrderRepository _repo;
    private readonly IPublisher _publisher;

    public async Task<int> PlaceOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var orderId = await _repo.SaveAsync(order, cancellationToken);

        // Publish after successful save
        await _publisher.Publish(
            new OrderPlacedNotification(orderId, order.Total),
            cancellationToken);

        return orderId;
    }
}
```

**Event Sourcing:**

```csharp
public class EventStoreHandler : INotificationHandler<OrderPlacedNotification>
{
    private readonly IEventStore _eventStore;

    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        return _eventStore.AppendAsync(
            streamId: $"order-{notification.OrderId}",
            eventType: "OrderPlaced",
            data: notification,
            cancellationToken);
    }
}
```

**Audit Logging:**

```csharp
public class AuditLogHandler : INotificationHandler<INotification>
{
    private readonly IAuditLog _auditLog;

    public Task Handle(INotification notification, CancellationToken cancellationToken)
    {
        return _auditLog.LogAsync(new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = notification.GetType().Name,
            Data = JsonSerializer.Serialize(notification)
        }, cancellationToken);
    }
}
```

---

## Testing Notifications

### Testing Handlers

Test handlers in isolation:

```csharp
[Fact]
public async Task EmailHandler_SendsConfirmationEmail()
{
    // Arrange
    var emailService = Substitute.For<IEmailService>();
    var handler = new EmailConfirmationHandler(emailService);
    var notification = new OrderPlacedNotification(123, 99.99m, "test@example.com");

    // Act
    await handler.Handle(notification, CancellationToken.None);

    // Assert
    await emailService.Received(1).SendConfirmationAsync("test@example.com", 123, Arg.Any<CancellationToken>());
}
```

### Testing Publishing

Test that notifications are published:

```csharp
[Fact]
public async Task PlaceOrder_PublishesOrderPlacedNotification()
{
    // Arrange
    var publisher = Substitute.For<IPublisher>();
    var service = new OrderService(publisher);

    // Act
    await service.CreateOrderAsync(new CreateOrderRequest { Total = 99.99m });

    // Assert
    await publisher.Received(1).Publish(
        Arg.Is<OrderPlacedNotification>(n => n.Total == 99.99m),
        Arg.Any<CancellationToken>());
}
```

---

## Comparison: Notifications vs Requests

| Feature | Notifications | Requests |
|---------|--------------|----------|
| Handlers | Multiple (0+) | Exactly one |
| Return value | None | Required |
| Execution | Sequential or parallel | Always sequential |
| Use case | Broadcasting events | Request/response operations |
| Interface | `INotificationHandler<>` | `IRequestHandler<,>` |
| Publisher | `IPublisher.Publish()` | `ISender.Send()` |

---

## Common Pitfalls

1. **Returning values from handlers:** Notification handlers cannot return meaningful values. Use requests instead.

2. **Handler order dependency:** With parallel publishers, don't rely on handler execution order.

3. **Shared mutable state:** Be careful when handlers modify shared state concurrently.

4. **Transaction boundaries:** Publishing inside a transaction might cause handlers to read uncommitted data.

5. **Synchronous blocking:** Don't use `.Result` or `.Wait()` in handlers - always use async/await.

---

## Next Steps

- **[Pipeline Behaviors](./04-behaviors.md)** - Add cross-cutting concerns to your requests
- **[Configuration](./05-configuration.md)** - Advanced configuration options
- **[Exception Handling](./06-exception-handling.md)** - Handle exceptions gracefully
