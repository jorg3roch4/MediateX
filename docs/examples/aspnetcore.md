# ASP.NET Core Integration Example

This example demonstrates how to integrate MediateX with ASP.NET Core using the built-in Microsoft.Extensions.DependencyInjection container.

## Overview

The ASP.NET Core example shows:
- Registration with Microsoft DI container
- Request/Response pattern (Ping/Pong)
- Void requests (Jing)
- Notifications (Pinged/Ponged)
- Stream requests (Sing/Song)
- Pipeline behaviors
- Pre and post processors
- Exception handling

## Source Code

📁 **Location:** [`samples/MediateX.Examples.AspNetCore/`](../../samples/MediateX.Examples.AspNetCore/)

## Setup

### 1. Register MediateX

```csharp
using MediateX;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register MediateX and scan assemblies for handlers
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Ping).Assembly,
        typeof(Sing).Assembly
    );
});

// Register stream handler explicitly (if needed)
services.AddScoped(typeof(IStreamRequestHandler<Sing, Song>), typeof(SingHandler));

// Register pipeline behaviors
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(GenericPipelineBehavior<,>));
services.AddScoped(typeof(IRequestPreProcessor<>), typeof(GenericRequestPreProcessor<>));
services.AddScoped(typeof(IRequestPostProcessor<,>), typeof(GenericRequestPostProcessor<,>));
services.AddScoped(typeof(IStreamPipelineBehavior<,>), typeof(GenericStreamPipelineBehavior<,>));

var serviceProvider = services.BuildServiceProvider();
var mediator = serviceProvider.GetRequiredService<IMediator>();
```

### 2. Define Requests and Handlers

```csharp
// Request with response
public class Ping : IRequest<Pong>
{
    public string Message { get; set; }
}

public class PingHandler : IRequestHandler<Ping, Pong>
{
    public async Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        return new Pong { Message = request.Message + " Pong" };
    }
}
```

### 3. Use the Mediator

```csharp
// Send request
var pong = await mediator.Send(new Ping { Message = "Ping" });
Console.WriteLine(pong.Message); // Output: Ping Pong

// Publish notification
await mediator.Publish(new Pinged());

// Stream data
await foreach (var song in mediator.CreateStream(new Sing { Message = "Sing" }))
{
    Console.WriteLine(song.Message);
}
```

## Key Features Demonstrated

### ✅ Request/Response Pattern
Handles single request with single handler returning a response.

### ✅ Void Requests
Commands that don't return a value using `IRequest` (without type parameter).

### ✅ Notifications (Pub/Sub)
Single notification handled by multiple handlers in sequence or parallel.

### ✅ Streaming Requests
Uses `IAsyncEnumerable<T>` for streaming data scenarios.

### ✅ Pipeline Behaviors
Wraps request handling with cross-cutting concerns:
- Logging
- Timing
- Validation
- Transaction management

### ✅ Pre/Post Processors
Execute logic before and after handler execution.

### ✅ Exception Handling
Graceful error handling with dedicated exception handlers.

## Running the Example

```bash
cd samples/MediateX.Examples.AspNetCore
dotnet run
```

## Expected Output

```
===============
ASP.NET Core DI
===============

Sending Ping...
- Starting Up
-- Handling Request
--- Handled Ping: Ping
-- Finished Request
- All Done
- All Done with Ping
Received: Ping Pong

Publishing Pinged...
Got pinged async.
Got pinged also async.
Got pinged constrained async.
Got notified.

...
```

## Notes

- This is the **recommended approach** for ASP.NET Core applications
- Uses the built-in DI container (no third-party dependencies)
- Fully supports scoped services and async patterns
- Compatible with minimal APIs and MVC

## Related Documentation

- [Quick Start Guide](../quick-start.md)
- [Pipeline Behaviors](../pipeline-behaviors.md)
- [All Examples](../../samples/)
