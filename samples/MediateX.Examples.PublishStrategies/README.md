# MediateX.Examples.PublishStrategies

Demonstrates different notification publishing strategies with MediateX.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.PublishStrategies
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Main entry point that tests all strategies |
| `Publisher.cs` | Custom publisher implementing multiple strategies |
| `PublishStrategy.cs` | Enum defining available strategies |
| `CustomMediator.cs` | Custom mediator that accepts a publishing delegate |
| `Pinged.cs` | Sample notification |
| `SyncPingedHandler.cs` | Synchronous handler (throws exception) |
| `AsyncPingedHandler.cs` | Asynchronous handler |

## Available Strategies

| Strategy | Behavior |
|----------|----------|
| `SyncContinueOnException` | Sequential execution. Continues on exception. Aggregates all exceptions. |
| `SyncStopOnException` | Sequential execution. Stops at first exception. |
| `Async` | Parallel execution with `Task.WhenAll`. Aggregates exceptions. |
| `ParallelNoWait` | Fire-and-forget. Returns immediately. Cannot capture exceptions. |
| `ParallelWhenAll` | Parallel execution. Waits for all handlers to complete. |
| `ParallelWhenAny` | Parallel execution. Returns when first handler completes. |

## Usage

```csharp
var publisher = provider.GetRequiredService<Publisher>();
var notification = new Pinged();

// Use specific strategy
await publisher.Publish(notification, PublishStrategy.ParallelWhenAll);

// Or use default strategy
publisher.DefaultStrategy = PublishStrategy.SyncContinueOnException;
await publisher.Publish(notification);
```

## Notes

- This example shows how to implement custom publishing strategies beyond the built-in `ForeachAwaitPublisher` and `TaskWhenAllPublisher`
- `CustomMediator` accepts a delegate for custom notification dispatch logic
- Useful for scenarios requiring fire-and-forget, race conditions, or custom error handling
- Each strategy has different exception handling behavior - choose based on your requirements
