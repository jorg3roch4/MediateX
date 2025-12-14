# Requests & Handlers

This guide covers everything you need to know about requests and handlers in MediateX.

---

## Table of Contents

- [Request Types](#request-types)
- [Handler Types](#handler-types)
- [Request Guidelines](#request-guidelines)
- [Handler Guidelines](#handler-guidelines)
- [Advanced Patterns](#advanced-patterns)

---

## Request Types

### IRequest<TResponse>

Represents a request that returns a typed response.

```csharp
public record GetProductQuery(int ProductId) : IRequest<Product>;
public record CreateUserCommand(string Name, string Email) : IRequest<int>; // Returns user ID
public record CalculateTotalQuery(List<int> ItemIds) : IRequest<decimal>;
```

**Use when:** You need a return value from the operation.

### IRequest

Represents a request with no return value (void).

```csharp
public record SendEmailCommand(string To, string Subject, string Body) : IRequest;
public record LogEventCommand(string Message, LogLevel Level) : IRequest;
public record DeleteProductCommand(int ProductId) : IRequest;
```

**Use when:** The operation doesn't need to return data (fire-and-forget commands).

### IRequest<Unit>

Alternative to `IRequest` for consistency.

```csharp
public record UpdateSettingsCommand(Dictionary<string, string> Settings) : IRequest<Unit>;
```

**Use when:** You want consistent return types across all requests.

---

## Handler Types

### IRequestHandler<TRequest, TResponse>

Handles requests that return a response.

```csharp
public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<GetProductHandler> _logger;

    public GetProductHandler(IProductRepository repository, ILogger<GetProductHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching product {ProductId}", request.ProductId);

        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product == null)
            throw new NotFoundException($"Product {request.ProductId} not found");

        return product;
    }
}
```

### IRequestHandler<TRequest>

Handles requests with no return value.

```csharp
public class SendEmailHandler : IRequestHandler<SendEmailCommand>
{
    private readonly IEmailService _emailService;

    public SendEmailHandler(IEmailService emailService)
        => _emailService = emailService;

    public async Task Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        await _emailService.SendAsync(
            request.To,
            request.Subject,
            request.Body,
            cancellationToken);
    }
}
```

---

## Request Guidelines

### Use Records

Records provide immutability and value-based equality:

```csharp
// ✅ Good - using record
public record GetUserQuery(int UserId) : IRequest<User>;

// ❌ Avoid - using class
public class GetUserQuery : IRequest<User>
{
    public int UserId { get; set; }
}
```

> **Warning: Primary Constructors vs Records (C# 12+)**
>
> If you're using C# 12 or later, be aware that **classes with primary constructors** behave differently from **records** and may cause registration issues.
>
> ```csharp
> // ❌ May cause issues - class with primary constructor
> public class GetUserQuery(int userId) : IRequest<User>
> {
>     public int UserId { get; } = userId;
> }
>
> // ✅ Recommended - record with primary constructor
> public record GetUserQuery(int UserId) : IRequest<User>;
> ```
>
> **Why this happens:**
> - Classes with primary constructors lack auto-generated value-based equality
> - Framework reflection may not properly resolve types with primary constructor syntax
> - Records are specifically designed for immutable data transfer objects
>
> **Best practice:** Always use `record` (or `record struct`) for requests and queries. They provide:
> - Immutability by default
> - Value-based equality (useful for caching and deduplication)
> - Concise syntax with primary constructors
> - Better compatibility with dependency injection and reflection-based frameworks

### Keep Requests Simple

Requests should be data containers, not behavior:

```csharp
// ✅ Good - simple data
public record CreateOrderCommand(
    int CustomerId,
    List<OrderItem> Items,
    string ShippingAddress) : IRequest<int>;

// ❌ Bad - contains behavior
public record CreateOrderCommand(int CustomerId) : IRequest<int>
{
    public decimal CalculateTotal() => /* logic */;
    public bool IsValid() => /* validation */;
}
```

### Don't Inject Dependencies in Requests

```csharp
// ❌ Bad - injecting services
public record ProcessOrderCommand : IRequest
{
    public ProcessOrderCommand(IOrderService orderService)
    {
        _orderService = orderService;
    }
}

// ✅ Good - pure data
public record ProcessOrderCommand(int OrderId) : IRequest;
```

### Use Descriptive Names

Follow naming conventions:

```csharp
// Queries (read operations) - end with "Query"
public record GetProductQuery(int Id) : IRequest<Product>;
public record SearchProductsQuery(string Term) : IRequest<List<Product>>;
public record GetOrdersByCustomerQuery(int CustomerId) : IRequest<List<Order>>;

// Commands (write operations) - end with "Command"
public record CreateProductCommand(string Name, decimal Price) : IRequest<int>;
public record UpdateProductCommand(int Id, string Name) : IRequest;
public record DeleteProductCommand(int Id) : IRequest;
```

---

## Handler Guidelines

### Single Responsibility

Each handler should do one thing:

```csharp
// ✅ Good - focused responsibility
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, int>
{
    public async Task<int> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = new Order { /* map from request */ };
        return await _repository.CreateAsync(order, cancellationToken);
    }
}

// ❌ Bad - too many responsibilities
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, int>
{
    public async Task<int> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Validates
        // Sends email
        // Updates inventory
        // Creates order
        // Logs
        // Caches
        // Too much!
    }
}
```

**Use behaviors** for cross-cutting concerns like validation, logging, etc.

### Keep Handlers Stateless

```csharp
// ✅ Good - stateless
public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    private readonly IProductRepository _repository; // Dependency, not state

    public async Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.ProductId, cancellationToken);
    }
}

// ❌ Bad - stateful
public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    private Product _lastProduct; // Mutable state - bad!

    public async Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        _lastProduct = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        return _lastProduct;
    }
}
```

### Always Use CancellationToken

```csharp
// ✅ Good - respects cancellation
public async Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
{
    var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
    var orders = await _orderRepository.GetByUserAsync(request.UserId, cancellationToken);
    return user;
}

// ❌ Bad - ignores cancellation
public async Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
{
    var user = await _repository.GetByIdAsync(request.UserId); // Missing token
    var orders = await _orderRepository.GetByUserAsync(request.UserId); // Missing token
    return user;
}
```

### Inject Dependencies via Constructor

```csharp
public class CreateProductHandler : IRequestHandler<CreateProductCommand, int>
{
    private readonly IProductRepository _repository;
    private readonly IValidator<CreateProductCommand> _validator;
    private readonly ILogger<CreateProductHandler> _logger;

    // Constructor injection
    public CreateProductHandler(
        IProductRepository repository,
        IValidator<CreateProductCommand> validator,
        ILogger<CreateProductHandler> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Use injected dependencies
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        _logger.LogInformation("Creating product {Name}", request.Name);

        var product = new Product { Name = request.Name, Price = request.Price };
        return await _repository.CreateAsync(product, cancellationToken);
    }
}
```

---

## Advanced Patterns

### Generic Handlers

Handle multiple request types with a single handler:

```csharp
public record DeleteEntityCommand<T>(int Id) : IRequest where T : class;

public class DeleteEntityHandler<T> : IRequestHandler<DeleteEntityCommand<T>>
    where T : class
{
    private readonly IRepository<T> _repository;

    public DeleteEntityHandler(IRepository<T> repository)
        => _repository = repository;

    public async Task Handle(DeleteEntityCommand<T> request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}

// Usage
await mediator.Send(new DeleteEntityCommand<Product>(123));
await mediator.Send(new DeleteEntityCommand<User>(456));
```

**Note:** Requires `cfg.RegisterGenericHandlers = true` in configuration.

### Polymorphic Requests

Base request with multiple handlers:

```csharp
public abstract record ExportCommand : IRequest<byte[]>
{
    public string FileName { get; init; }
}

public record ExportPdfCommand(string FileName) : ExportCommand;
public record ExportExcelCommand(string FileName) : ExportCommand;

public class ExportPdfHandler : IRequestHandler<ExportPdfCommand, byte[]>
{
    public Task<byte[]> Handle(ExportPdfCommand request, CancellationToken cancellationToken)
    {
        // Generate PDF
        return Task.FromResult(new byte[0]);
    }
}

public class ExportExcelHandler : IRequestHandler<ExportExcelCommand, byte[]>
{
    public Task<byte[]> Handle(ExportExcelCommand request, CancellationToken cancellationToken)
    {
        // Generate Excel
        return Task.FromResult(new byte[0]);
    }
}
```

### Request with Validation

```csharp
public record CreateUserCommand(string Name, string Email, int Age) : IRequest<int>;

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).GreaterThan(0).LessThan(150);
    }
}

public class CreateUserHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly IUserRepository _repository;
    private readonly IValidator<CreateUserCommand> _validator;

    public CreateUserHandler(IUserRepository repository, IValidator<CreateUserCommand> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Validate
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        // Create user
        var user = new User { Name = request.Name, Email = request.Email, Age = request.Age };
        return await _repository.CreateAsync(user, cancellationToken);
    }
}
```

**Better approach:** Use a validation behavior (see [Pipeline Behaviors](./04-behaviors.md)).

### Dynamic Dispatch

Send requests using `object` type:

```csharp
object request = new GetProductQuery(42);
var result = await mediator.Send(request); // Returns object?

// Cast result
var product = (Product)result;
```

**Use sparingly:** Prefer strongly-typed `Send<TResponse>()` when possible.

---

## Best Practices Summary

### Requests
- ✅ Use `record` instead of `class` for immutability
- ✅ Avoid classes with primary constructors (C# 12+) - use records instead
- ✅ Keep them simple (data only)
- ✅ Use descriptive names (Query/Command suffix)
- ✅ No dependencies injection
- ✅ No business logic

### Handlers
- ✅ Single responsibility
- ✅ Stateless
- ✅ Constructor injection for dependencies
- ✅ Always use CancellationToken
- ✅ One handler per request type
- ✅ Throw meaningful exceptions

---

## Next Steps

- **[Notifications & Events](./03-notifications.md)** - Publish to multiple handlers
- **[Pipeline Behaviors](./04-behaviors.md)** - Add cross-cutting concerns
- **[Exception Handling](./06-exception-handling.md)** - Handle errors gracefully

