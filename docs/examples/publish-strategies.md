# Notification Publishing Strategies

This example demonstrates different strategies for publishing notifications to multiple handlers.

## Overview

**📁 Location:** [`samples/MediateX.Examples.PublishStrategies/`](../../samples/MediateX.Examples.PublishStrategies/)

When you publish a notification with multiple handlers, MediateX supports different strategies for executing those handlers.

## Built-in Strategies

### 1. ForeachAwaitPublisher (Default)

Executes handlers **sequentially**, awaiting each one before moving to the next.

```csharp
using MediateX;

// This is the default - no configuration needed
var mediator = serviceProvider.GetRequiredService<IMediator>();

// Or configure explicitly
services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new ForeachAwaitPublisher();
});
```

**Characteristics:**
- ✅ Handlers execute in order
- ✅ Preserves execution sequence
- ✅ Easy to reason about
- ⚠️ Slower if handlers are independent
- ⚠️ One slow handler blocks others

**Use when:**
- Order of execution matters
- Handlers have dependencies on each other
- You need predictable, sequential processing

---

### 2. TaskWhenAllPublisher

Executes all handlers **in parallel** using `Task.WhenAll`.

```csharp
using MediateX;

services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new TaskWhenAllPublisher();
});
```

**Characteristics:**
- ✅ Maximum throughput
- ✅ Faster for independent handlers
- ✅ All handlers start immediately
- ⚠️ No guaranteed order
- ⚠️ All must complete before returning

**Use when:**
- Handlers are independent
- Performance is critical
- Order doesn't matter
- Handlers can run concurrently

---

## Custom Publishing Strategies

You can create custom publishing strategies by implementing `INotificationPublisher`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediateX;

public class FireAndForgetPublisher : INotificationPublisher
{
    public Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        // Fire all handlers without awaiting
        foreach (var handler in handlerExecutors)
        {
            _ = Task.Run(() =>
                handler.HandlerCallback(notification, cancellationToken),
                cancellationToken
            );
        }

        return Task.CompletedTask;
    }
}
```

### Custom Strategy Examples

#### Priority-Based Publisher

Execute high-priority handlers first:

```csharp
public class PriorityPublisher : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        var handlers = handlerExecutors
            .OrderByDescending(h => GetPriority(h.HandlerInstance))
            .ToList();

        foreach (var handler in handlers)
        {
            await handler.HandlerCallback(notification, cancellationToken);
        }
    }

    private int GetPriority(object handler)
    {
        // Implement priority logic (attributes, interfaces, etc.)
        return handler is IHighPriorityHandler ? 10 : 0;
    }
}
```

#### Batching Publisher

Group handlers into batches and execute batches in parallel:

```csharp
public class BatchingPublisher : INotificationPublisher
{
    private readonly int _batchSize;

    public BatchingPublisher(int batchSize = 5)
    {
        _batchSize = batchSize;
    }

    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        var handlers = handlerExecutors.ToList();

        for (int i = 0; i < handlers.Count; i += _batchSize)
        {
            var batch = handlers.Skip(i).Take(_batchSize);
            var tasks = batch.Select(h =>
                h.HandlerCallback(notification, cancellationToken)
            );

            await Task.WhenAll(tasks);
        }
    }
}
```

#### Stop-On-Exception Publisher

Stop processing if any handler throws:

```csharp
public class StopOnExceptionPublisher : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            try
            {
                await handler.HandlerCallback(notification, cancellationToken);
            }
            catch
            {
                // Stop on first exception
                throw;
            }
        }
    }
}
```

## Configuration

### Global Configuration

Set the publisher for all notifications:

```csharp
services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new TaskWhenAllPublisher();
});
```

### Per-Mediator Configuration

Create a custom mediator with specific publisher:

```csharp
public class CustomMediator : Mediator
{
    public CustomMediator(IServiceProvider serviceProvider)
        : base(serviceProvider, new TaskWhenAllPublisher())
    {
    }
}

services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.MediatorImplementationType = typeof(CustomMediator);
});
```

### Runtime Publisher Selection

Override `PublishCore` for dynamic publisher selection:

```csharp
public class SmartMediator : Mediator
{
    private readonly ForeachAwaitPublisher _sequential = new();
    private readonly TaskWhenAllPublisher _parallel = new();

    protected override Task PublishCore(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        // Use parallel for certain notification types
        if (notification is IParallelNotification)
        {
            return _parallel.Publish(handlerExecutors, notification, cancellationToken);
        }

        return _sequential.Publish(handlerExecutors, notification, cancellationToken);
    }
}
```

## Performance Comparison

| Strategy | Execution | Performance | Ordering | Error Handling |
|----------|-----------|-------------|----------|----------------|
| **ForeachAwait** | Sequential | Slower | Guaranteed | First error stops |
| **TaskWhenAll** | Parallel | Faster | None | AggregateException |
| **FireAndForget** | Background | Fastest | None | Silent failures |
| **Priority** | Sequential | Medium | Custom | First error stops |
| **Batching** | Batch Parallel | Medium-Fast | Batch order | Per batch |

## Testing Publishers

```csharp
[Fact]
public async Task Should_Execute_Handlers_In_Parallel()
{
    var publisher = new TaskWhenAllPublisher();
    var executionTimes = new List<DateTime>();

    var handlers = new[]
    {
        new NotificationHandlerExecutor(
            new Handler1(),
            (n, ct) => { executionTimes.Add(DateTime.Now); return Task.CompletedTask; }
        ),
        new NotificationHandlerExecutor(
            new Handler2(),
            (n, ct) => { executionTimes.Add(DateTime.Now); return Task.CompletedTask; }
        )
    };

    await publisher.Publish(handlers, new TestNotification(), CancellationToken.None);

    // Handlers should start at nearly the same time
    (executionTimes[1] - executionTimes[0]).Should().BeLessThan(TimeSpan.FromMilliseconds(50));
}
```

## Running the Example

```bash
cd samples/MediateX.Examples.PublishStrategies
dotnet run
```

The example demonstrates different publishing strategies and their behavior.

## Best Practices

1. **Use ForeachAwait (default)** for most scenarios
2. **Use TaskWhenAll** when:
   - Handlers are independent
   - Performance is critical
   - Order doesn't matter
3. **Create custom publishers** for:
   - Priority-based execution
   - Batching
   - Retry logic
   - Circuit breakers
4. **Test your strategy** - Verify it behaves as expected

## See Also

- [Quick Start Guide](../quick-start.md)
- [ASP.NET Core Example](aspnetcore.md)
- [Pipeline Behaviors](../pipeline-behaviors.md)
