# Quick Start Guide

Get started with MediateX in minutes! This guide will walk you through the basics of implementing the mediator pattern in your .NET 9+ application.

## Installation

Install MediateX via NuGet:

```bash
dotnet add package MediateX
```

Or using Package Manager:

```powershell
Install-Package MediateX
```

## Step 1: Register MediateX

In your `Program.cs` or startup configuration, register MediateX services:

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register MediateX and scan for handlers
builder.Services.AddMediateX(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>()
);

var app = builder.Build();
app.Run();
```

## Step 2: Create a Request

Define a request that returns a response:

```csharp
using MediateX;

public class GetWeatherQuery : IRequest<WeatherForecast>
{
    public string City { get; set; }
}

public class WeatherForecast
{
    public string City { get; set; }
    public int Temperature { get; set; }
    public string Condition { get; set; }
}
```

## Step 3: Create a Handler

Implement a handler for your request:

```csharp
using System.Threading;
using System.Threading.Tasks;
using MediateX;

public class GetWeatherQueryHandler : IRequestHandler<GetWeatherQuery, WeatherForecast>
{
    public async Task<WeatherForecast> Handle(GetWeatherQuery request, CancellationToken cancellationToken)
    {
        // Simulate fetching weather data
        await Task.Delay(100, cancellationToken);

        return new WeatherForecast
        {
            City = request.City,
            Temperature = 72,
            Condition = "Sunny"
        };
    }
}
```

## Step 4: Use the Mediator

Inject `IMediator` and send requests:

```csharp
using MediateX;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeatherController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{city}")]
    public async Task<ActionResult<WeatherForecast>> GetWeather(string city)
    {
        var weather = await _mediator.Send(new GetWeatherQuery { City = city });
        return Ok(weather);
    }
}
```

## Void Requests (Commands)

For requests that don't return a value, use `IRequest` without a type parameter:

```csharp
using MediateX;

public class SendEmailCommand : IRequest
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}

public class SendEmailCommandHandler : IRequestHandler<SendEmailCommand>
{
    public async Task Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        // Send email logic here
        await Task.CompletedTask;
    }
}

// Usage
await _mediator.Send(new SendEmailCommand
{
    To = "user@example.com",
    Subject = "Welcome!",
    Body = "Thanks for signing up!"
});
```

## Notifications (Pub/Sub)

Send notifications to multiple handlers:

```csharp
using MediateX;

public class OrderPlacedNotification : INotification
{
    public int OrderId { get; set; }
    public decimal Total { get; set; }
}

// Multiple handlers can handle the same notification
public class SendConfirmationEmailHandler : INotificationHandler<OrderPlacedNotification>
{
    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Send confirmation email
    }
}

public class UpdateInventoryHandler : INotificationHandler<OrderPlacedNotification>
{
    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Update inventory
    }
}

// Usage - all handlers are called
await _mediator.Publish(new OrderPlacedNotification
{
    OrderId = 123,
    Total = 99.99m
});
```

## Next Steps

- **[Pipeline Behaviors](pipeline-behaviors.md)** - Add cross-cutting concerns like logging and validation
- **[Advanced Patterns](advanced-patterns.md)** - Streaming, generic handlers, and more
- **[Examples](../samples/)** - Explore sample projects for different DI containers

## Common Patterns

### CQRS (Command Query Responsibility Segregation)

```csharp
// Commands (modify state)
public class CreateUserCommand : IRequest<int> // Returns user ID
{
    public string Name { get; set; }
    public string Email { get; set; }
}

// Queries (read state)
public class GetUserQuery : IRequest<User>
{
    public int UserId { get; set; }
}
```

### Unit Type for Void Returns

Use `Unit` when you need a consistent return type:

```csharp
using MediateX;

public class LogEventCommand : IRequest<Unit>
{
    public string Message { get; set; }
}

public class LogEventCommandHandler : IRequestHandler<LogEventCommand, Unit>
{
    public async Task<Unit> Handle(LogEventCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine(request.Message);
        return Unit.Value;
    }
}
```

## Tips

- **Keep handlers focused** - Each handler should have a single responsibility
- **Use behaviors for cross-cutting concerns** - Don't repeat validation/logging in every handler
- **Inject dependencies into handlers** - Not into requests
- **Use CancellationToken** - Always pass it through for proper cancellation support
- **Consider ISender/IPublisher** - Use specific interfaces when you only need Send or Publish

## Troubleshooting

### Handlers not found

Make sure you're scanning the correct assembly:

```csharp
// Scan assembly containing a specific type
cfg.RegisterServicesFromAssemblyContaining<MyHandler>();

// Scan specific assembly
cfg.RegisterServicesFromAssembly(typeof(MyHandler).Assembly);

// Scan multiple assemblies
cfg.RegisterServicesFromAssemblies(
    typeof(Handler1).Assembly,
    typeof(Handler2).Assembly
);
```

### Multiple handlers registered

This is expected for `INotificationHandler<>` - all handlers will be called. For `IRequestHandler<>`, only one handler should exist per request type.

---

Ready to dive deeper? Check out the [full documentation](README.md) and [examples](../samples/).
