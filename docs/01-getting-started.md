# Getting Started with MediateX

Get up and running with MediateX in minutes! This guide will walk you through installation, basic setup, and your first request/response.

---

## Installation

Install MediateX via NuGet:

```bash
dotnet add package MediateX
```

Or using Package Manager Console:

```powershell
Install-Package MediateX
```

**Requirements:**
- .NET 10.0 or higher
- C# 14

---

## Basic Setup

### 1. Register MediateX

In your `Program.cs`, register MediateX and tell it which assembly contains your handlers:

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register MediateX
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();
app.Run();
```

### 2. Create Your First Request

Define a request using the `IRequest<TResponse>` interface:

```csharp
using MediateX;

public record GetWeatherQuery(string City) : IRequest<WeatherForecast>;

public record WeatherForecast(string City, int Temperature, string Condition);
```

### 3. Create a Handler

Implement `IRequestHandler<TRequest, TResponse>` to handle your request:

```csharp
using MediateX;

public class GetWeatherHandler : IRequestHandler<GetWeatherQuery, WeatherForecast>
{
    public async Task<WeatherForecast> Handle(
        GetWeatherQuery request,
        CancellationToken cancellationToken)
    {
        // Simulate fetching weather data
        await Task.Delay(100, cancellationToken);

        return new WeatherForecast(request.City, 72, "Sunny");
    }
}
```

### 4. Send the Request

Inject `IMediator` and send your request.

#### Minimal API (Recommended)

```csharp
// Map endpoints using Minimal API
app.MapGet("/weather/{city}", async (string city, IMediator mediator) =>
{
    var forecast = await mediator.Send(new GetWeatherQuery(city));
    return Results.Ok(forecast);
});

app.Run();
```

#### Controller-Based API

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

### Requests with Response

Use `IRequest<TResponse>` when you need a return value:

```csharp
// Query example
public record GetUserQuery(int UserId) : IRequest<User>;

public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _repo;

    public GetUserHandler(IUserRepository repo) => _repo = repo;

    public Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
        => _repo.GetByIdAsync(request.UserId, cancellationToken);
}

// Usage
var user = await mediator.Send(new GetUserQuery(123));
```

### Requests without Response (Commands)

Use `IRequest` (no generic parameter) for void operations:

```csharp
// Command example
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

// Usage
await mediator.Send(new CreateUserCommand("John Doe", "john@example.com"));
```

### Using Unit Type

Alternatively, use `Unit` for consistent return types:

```csharp
public record DeleteUserCommand(int UserId) : IRequest<Unit>;

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // Delete user logic
        return Unit.Value; // or: return await Unit.Task;
    }
}
```

---

## Notifications (Pub/Sub)

Notifications allow you to publish events to multiple handlers.

### Define a Notification

```csharp
public record OrderPlacedNotification(int OrderId, decimal Total) : INotification;
```

### Create Multiple Handlers

```csharp
// Handler 1: Send confirmation email
public class EmailHandler : INotificationHandler<OrderPlacedNotification>
{
    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Send email logic
        await Task.CompletedTask;
    }
}

// Handler 2: Update inventory
public class InventoryHandler : INotificationHandler<OrderPlacedNotification>
{
    public async Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        // Update inventory logic
        await Task.CompletedTask;
    }
}

// Handler 3: Log event
public class AuditHandler : INotificationHandler<OrderPlacedNotification>
{
    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order {notification.OrderId} placed for ${notification.Total}");
        return Task.CompletedTask;
    }
}
```

### Publish the Notification

```csharp
// All handlers will be invoked
await mediator.Publish(new OrderPlacedNotification(123, 99.99m));
```

---

## Mediator Interfaces

MediateX provides three interfaces for different use cases:

### IMediator
Combines both `ISender` and `IPublisher`. Use when you need both capabilities.

```csharp
public class OrderService
{
    private readonly IMediator _mediator;

    public async Task ProcessOrder(int orderId)
    {
        var order = await _mediator.Send(new GetOrderQuery(orderId));
        await _mediator.Publish(new OrderProcessed(orderId));
    }
}
```

### ISender
Use when you only need request/response (Send operations).

```csharp
public class QueryService
{
    private readonly ISender _sender;

    public Task<Product> GetProduct(int id)
        => _sender.Send(new GetProductQuery(id));
}
```

### IPublisher
Use when you only need to publish notifications.

```csharp
public class EventPublisher
{
    private readonly IPublisher _publisher;

    public Task PublishEvent(INotification notification)
        => _publisher.Publish(notification);
}
```

---

## CQRS Pattern

MediateX naturally supports Command Query Responsibility Segregation (CQRS):

```csharp
// === COMMANDS (Modify State) ===
public record CreateProductCommand(string Name, decimal Price) : IRequest<int>; // Returns ID

public class CreateProductHandler : IRequestHandler<CreateProductCommand, int>
{
    public Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Create product and return ID
        return Task.FromResult(42);
    }
}

// === QUERIES (Read State) ===
public record GetProductQuery(int Id) : IRequest<Product>;

public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    public Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Fetch and return product
        return Task.FromResult(new Product());
    }
}
```

---

## Complete Minimal API Example

Here's a complete example using Minimal API with MediateX:

```csharp
using MediateX;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();

// === COMMANDS ===
app.MapPost("/products", async (
    [FromBody] CreateProductRequest request,
    IMediator mediator) =>
{
    var command = new CreateProductCommand(request.Name, request.Price);
    var productId = await mediator.Send(command);
    return Results.Created($"/products/{productId}", new { id = productId });
});

app.MapPut("/products/{id}", async (
    int id,
    [FromBody] UpdateProductRequest request,
    IMediator mediator) =>
{
    var command = new UpdateProductCommand(id, request.Name, request.Price);
    await mediator.Send(command);
    return Results.NoContent();
});

// === QUERIES ===
app.MapGet("/products/{id}", async (
    int id,
    IMediator mediator) =>
{
    var query = new GetProductQuery(id);
    var product = await mediator.Send(query);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.MapGet("/products", async (
    [FromQuery] string? category,
    IMediator mediator) =>
{
    var query = new GetProductsQuery(category);
    var products = await mediator.Send(query);
    return Results.Ok(products);
});

// === EVENTS ===
app.MapPost("/orders/{id}/complete", async (
    int id,
    IMediator mediator) =>
{
    // Complete the order first
    var command = new CompleteOrderCommand(id);
    await mediator.Send(command);

    // Then publish event to notify all interested parties
    await mediator.Publish(new OrderCompletedNotification(id));

    return Results.Ok();
});

app.Run();

// Request DTOs for Minimal API
record CreateProductRequest(string Name, decimal Price);
record UpdateProductRequest(string Name, decimal Price);
```

---

## Dependency Injection

Handlers support constructor injection:

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

**Important:** Inject dependencies into **handlers**, not requests. Requests should be simple data containers.

---

## Best Practices

### ✅ Do's

- **Use records for requests** - Immutable and concise
- **Keep handlers focused** - Single responsibility per handler
- **Pass CancellationToken** - Always forward to async operations
- **Inject dependencies in handlers** - Not in requests
- **Use descriptive names** - `GetProductQuery`, `CreateOrderCommand`
- **Return specific types** - Avoid `object` or `dynamic`

### ❌ Don'ts

- **Don't inject services in requests** - Requests are data, not behavior
- **Don't share state in handlers** - Keep handlers stateless
- **Don't ignore CancellationToken** - Always respect cancellation
- **Don't create multiple handlers for same request** - One request = one handler
- **Don't put business logic in controllers** - Move it to handlers

---

## Common Patterns

### Repository Pattern

```csharp
public record GetAllProductsQuery : IRequest<List<Product>>;

public class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, List<Product>>
{
    private readonly IProductRepository _repository;

    public GetAllProductsHandler(IProductRepository repository)
        => _repository = repository;

    public Task<List<Product>> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken)
        => _repository.GetAllAsync(cancellationToken);
}
```

### Service Layer Pattern

```csharp
public record ProcessPaymentCommand(int OrderId, decimal Amount) : IRequest<bool>;

public class ProcessPaymentHandler : IRequestHandler<ProcessPaymentCommand, bool>
{
    private readonly IPaymentService _paymentService;

    public ProcessPaymentHandler(IPaymentService paymentService)
        => _paymentService = paymentService;

    public async Task<bool> Handle(
        ProcessPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.ChargeAsync(
            request.OrderId,
            request.Amount,
            cancellationToken);

        return result.Success;
    }
}
```

---

## Troubleshooting

### Handler Not Found

**Problem:** `InvalidOperationException: Handler was not found for request`

**Solutions:**
- Ensure handler assembly is registered: `cfg.RegisterServicesFromAssemblyContaining<YourHandler>()`
- Verify handler implements correct interface
- Check handler class is public and not abstract
- Confirm handler is in registered assembly

### Multiple Handlers Error

**Problem:** Multiple handlers found for a request

**Solution:** Only one handler should implement `IRequestHandler<TRequest, TResponse>` for each request type. Use `INotificationHandler<>` if you need multiple handlers.

### Dependency Not Resolved

**Problem:** Handler dependencies not injected

**Solution:** Ensure all handler dependencies are registered in DI container before calling `AddMediateX()`.

---

## Next Steps

Now that you understand the basics, explore advanced features:

- **[Requests & Handlers](./02-requests-handlers.md)** - Deep dive into requests and handlers
- **[Notifications & Events](./03-notifications.md)** - Publishing strategies and event handling
- **[Pipeline Behaviors](./04-behaviors.md)** - Add cross-cutting concerns
- **[Configuration](./05-configuration.md)** - Complete configuration reference
- **[Exception Handling](./06-exception-handling.md)** - Advanced error handling
- **[Streaming](./07-streaming.md)** - Work with async streams

---

**Ready to build?** Start with simple requests and gradually add behaviors and advanced features as needed.
