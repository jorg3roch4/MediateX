# Getting Started

This guide covers installation, basic setup, and your first request/response.

---

## Installation

```bash
dotnet add package MediateX
```

**Requirements:** .NET 10.0+, C# 14

---

## Basic Setup

### 1. Register MediateX

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();
app.Run();
```

### 2. Create a Request

```csharp
public record GetWeatherQuery(string City) : IRequest<WeatherForecast>;

public record WeatherForecast(string City, int Temperature, string Condition);
```

### 3. Create a Handler

```csharp
public class GetWeatherHandler : IRequestHandler<GetWeatherQuery, WeatherForecast>
{
    public async Task<WeatherForecast> Handle(
        GetWeatherQuery request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        return new WeatherForecast(request.City, 72, "Sunny");
    }
}
```

### 4. Send the Request

**Minimal API:**

```csharp
app.MapGet("/weather/{city}", async (string city, IMediator mediator) =>
{
    var forecast = await mediator.Send(new GetWeatherQuery(city));
    return Results.Ok(forecast);
});
```

**Controller:**

```csharp
[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeatherController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{city}")]
    public async Task<ActionResult<WeatherForecast>> Get(string city)
    {
        var forecast = await _mediator.Send(new GetWeatherQuery(city));
        return Ok(forecast);
    }
}
```

---

## Request Types

### With Response

Use `IRequest<TResponse>` when you need a return value:

```csharp
public record GetUserQuery(int UserId) : IRequest<User>;

public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _repo;

    public GetUserHandler(IUserRepository repo) => _repo = repo;

    public Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
        => _repo.GetByIdAsync(request.UserId, cancellationToken);
}

var user = await mediator.Send(new GetUserQuery(123));
```

### Without Response (Void)

Use `IRequest` for operations that don't return data:

```csharp
public record CreateUserCommand(string Name, string Email) : IRequest;

public class CreateUserHandler : IRequestHandler<CreateUserCommand>
{
    private readonly IUserRepository _repo;

    public CreateUserHandler(IUserRepository repo) => _repo = repo;

    public async Task Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User { Name = request.Name, Email = request.Email };
        await _repo.AddAsync(user, cancellationToken);
    }
}

await mediator.Send(new CreateUserCommand("John Doe", "john@example.com"));
```

### Using Unit Type

If you want consistent return types across all requests:

```csharp
public record DeleteUserCommand(int UserId) : IRequest<Unit>;

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // Delete logic
        return Unit.Value;
    }
}
```

---

## Notifications (Pub/Sub)

Notifications go to multiple handlers. Requests go to one.

```csharp
public record OrderPlacedNotification(int OrderId, decimal Total) : INotification;

public class EmailHandler : INotificationHandler<OrderPlacedNotification>
{
    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Send email
        return Task.CompletedTask;
    }
}

public class InventoryHandler : INotificationHandler<OrderPlacedNotification>
{
    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Update inventory
        return Task.CompletedTask;
    }
}

// All handlers execute
await mediator.Publish(new OrderPlacedNotification(123, 99.99m));
```

---

## Interface Options

MediateX provides three interfaces:

| Interface | Use Case |
|-----------|----------|
| `IMediator` | Need both Send and Publish |
| `ISender` | Only need Send (request/response) |
| `IPublisher` | Only need Publish (notifications) |

I recommend using `ISender` or `IPublisher` when you only need one capability. It makes dependencies clearer.

```csharp
public class QueryService
{
    private readonly ISender _sender;

    public Task<Product> GetProduct(int id)
        => _sender.Send(new GetProductQuery(id));
}

public class EventService
{
    private readonly IPublisher _publisher;

    public Task PublishEvent(INotification notification)
        => _publisher.Publish(notification);
}
```

---

## CQRS Pattern

MediateX fits naturally with CQRS. Commands modify state, queries read it:

```csharp
// Command - modifies state
public record CreateProductCommand(string Name, decimal Price) : IRequest<int>;

// Query - reads state
public record GetProductQuery(int Id) : IRequest<Product>;
```

Name your requests accordingly: `*Query` for reads, `*Command` for writes.

---

## Dependency Injection

Handlers work with standard .NET DI:

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, int>
{
    private readonly IOrderRepository _orderRepo;
    private readonly IEmailService _emailService;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IOrderRepository orderRepo,
        IEmailService emailService,
        ILogger<CreateOrderHandler> logger)
    {
        _orderRepo = orderRepo;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<int> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order...");

        var order = new Order { /* ... */ };
        var orderId = await _orderRepo.CreateAsync(order, cancellationToken);

        await _emailService.SendConfirmationAsync(order.CustomerEmail);

        return orderId;
    }
}
```

Inject dependencies into handlers, not requests. Requests are just data.

---

## Guidelines

**Do:**
- Use records for requests (immutable, concise)
- Keep handlers focused on one thing
- Pass `CancellationToken` to all async operations
- Name requests clearly: `GetProductQuery`, `CreateOrderCommand`

**Don't:**
- Put services in requests
- Share state between handler calls
- Ignore `CancellationToken`
- Create multiple handlers for the same request type

---

## Troubleshooting

**Handler not found:**
- Check the assembly is registered: `cfg.RegisterServicesFromAssemblyContaining<YourHandler>()`
- Verify handler is public and not abstract
- Confirm handler implements the correct interface

**Dependencies not resolved:**
- Register handler dependencies before calling `AddMediateX()`

---

## Next Steps

- [Requests & Handlers](./02-requests-handlers.md) - More on request patterns
- [Notifications](./03-notifications.md) - Publishing strategies
- [Pipeline Behaviors](./04-behaviors.md) - Cross-cutting concerns
- [Configuration](./05-configuration.md) - All options
