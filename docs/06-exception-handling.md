# Exception Handling

MediateX provides powerful exception handling capabilities that allow you to handle exceptions gracefully within the request pipeline, without cluttering your handlers with try-catch blocks.

---

## Core Concepts

MediateX offers two approaches to exception handling:

1. **Exception Handlers** (`IRequestExceptionHandler<,,>`) - Handle exceptions and optionally provide a response
2. **Exception Actions** (`IRequestExceptionAction<,>`) - Perform side effects when exceptions occur (logging, alerting, etc.)

---

## Exception Handlers

Exception handlers can catch exceptions, perform recovery logic, and optionally provide a response to return instead of propagating the exception.

### IRequestExceptionHandler Interface

```csharp
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : notnull
    where TException : Exception
{
    Task Handle(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}
```

### RequestExceptionHandlerState

The state object allows you to mark an exception as handled and provide a response:

```csharp
public class RequestExceptionHandlerState<TResponse>
{
    public bool Handled { get; private set; }
    public TResponse? Response { get; private set; }

    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
```

### Basic Exception Handler

```csharp
public record GetProductQuery(int ProductId) : IRequest<Product>;

public class ProductNotFoundExceptionHandler
    : IRequestExceptionHandler<GetProductQuery, Product, ProductNotFoundException>
{
    private readonly ILogger<ProductNotFoundExceptionHandler> _logger;

    public ProductNotFoundExceptionHandler(ILogger<ProductNotFoundExceptionHandler> logger)
        => _logger = logger;

    public Task Handle(
        GetProductQuery request,
        ProductNotFoundException exception,
        RequestExceptionHandlerState<Product> state,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Product {ProductId} not found",
            request.ProductId);

        // Return a default product instead of throwing
        state.SetHandled(new Product
        {
            Id = request.ProductId,
            Name = "Product Not Found",
            IsAvailable = false
        });

        return Task.CompletedTask;
    }
}
```

---

## Exception Hierarchy Support

Exception handlers respect the exception type hierarchy. If no handler exists for a specific exception, MediateX will look for handlers of base exception types.

### Example: Exception Hierarchy

```csharp
// Exception hierarchy
public class DatabaseException : Exception { }
public class ConnectionException : DatabaseException { }
public class TimeoutException : DatabaseException { }

// Handler for base exception type
public class DatabaseExceptionHandler
    : IRequestExceptionHandler<GetProductQuery, Product, DatabaseException>
{
    private readonly ILogger<DatabaseExceptionHandler> _logger;

    public DatabaseExceptionHandler(ILogger<DatabaseExceptionHandler> logger)
        => _logger = logger;

    public Task Handle(
        GetProductQuery request,
        DatabaseException exception,
        RequestExceptionHandlerState<Product> state,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Database error occurred");

        // Handle all database exceptions
        state.SetHandled(Product.CreateUnavailable());

        return Task.CompletedTask;
    }
}

// Specific handler for connection exceptions
public class ConnectionExceptionHandler
    : IRequestExceptionHandler<GetProductQuery, Product, ConnectionException>
{
    private readonly ILogger<ConnectionExceptionHandler> _logger;

    public ConnectionExceptionHandler(ILogger<ConnectionExceptionHandler> logger)
        => _logger = logger;

    public Task Handle(
        GetProductQuery request,
        ConnectionException exception,
        RequestExceptionHandlerState<Product> state,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Database connection failed");

        // More specific handling for connection issues
        state.SetHandled(Product.CreateFromCache(request.ProductId));

        return Task.CompletedTask;
    }
}
```

**Execution order:**
1. MediateX first looks for `ConnectionExceptionHandler` (most specific)
2. If not handled, tries `DatabaseExceptionHandler` (base class)
3. If still not handled, tries any `Exception` handler
4. If no handler marks it as handled, the exception is re-thrown

---

## Multiple Exception Handlers

You can register multiple handlers for the same exception type. They execute in order until one marks the exception as handled.

```csharp
public class RetryableExceptionHandler
    : IRequestExceptionHandler<ProcessOrderCommand, OrderResult, HttpRequestException>
{
    private readonly ILogger<RetryableExceptionHandler> _logger;

    public async Task Handle(
        ProcessOrderCommand request,
        HttpRequestException exception,
        RequestExceptionHandlerState<OrderResult> state,
        CancellationToken cancellationToken)
    {
        if (request.RetryCount < 3)
        {
            _logger.LogWarning("Retrying order processing, attempt {Attempt}", request.RetryCount + 1);
            // Don't set as handled - let it retry
            return;
        }

        _logger.LogError(exception, "Max retries exceeded for order processing");
        state.SetHandled(OrderResult.Failed("Service temporarily unavailable"));
    }
}

public class FallbackExceptionHandler
    : IRequestExceptionHandler<ProcessOrderCommand, OrderResult, HttpRequestException>
{
    private readonly ILogger<FallbackExceptionHandler> _logger;

    public Task Handle(
        ProcessOrderCommand request,
        HttpRequestException exception,
        RequestExceptionHandlerState<OrderResult> state,
        CancellationToken cancellationToken)
    {
        // This will only execute if RetryableExceptionHandler didn't handle it
        _logger.LogError(exception, "Using fallback for order processing");
        state.SetHandled(OrderResult.Queued("Will retry later"));

        return Task.CompletedTask;
    }
}
```

---

## Exception Actions

Exception actions perform side effects when exceptions occur but don't prevent the exception from being thrown. They're useful for logging, alerting, or cleanup tasks.

### IRequestExceptionAction Interface

```csharp
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : notnull
    where TException : Exception
{
    Task Execute(
        TRequest request,
        TException exception,
        CancellationToken cancellationToken);
}
```

### Basic Exception Action

```csharp
public class LogExceptionAction : IRequestExceptionAction<IRequest, Exception>
{
    private readonly ILogger<LogExceptionAction> _logger;

    public LogExceptionAction(ILogger<LogExceptionAction> logger)
        => _logger = logger;

    public Task Execute(IRequest request, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Exception occurred while handling {RequestType}",
            request.GetType().Name);

        return Task.CompletedTask;
    }
}
```

### Advanced Exception Actions

**Alert on critical exceptions:**

```csharp
public class AlertExceptionAction : IRequestExceptionAction<IRequest, CriticalException>
{
    private readonly IAlertService _alertService;
    private readonly ILogger<AlertExceptionAction> _logger;

    public AlertExceptionAction(IAlertService alertService, ILogger<AlertExceptionAction> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    public async Task Execute(
        IRequest request,
        CriticalException exception,
        CancellationToken cancellationToken)
    {
        _logger.LogCritical(exception, "Critical exception occurred");

        await _alertService.SendAlertAsync(
            severity: AlertSeverity.Critical,
            message: $"Critical exception in {request.GetType().Name}",
            exception: exception,
            cancellationToken: cancellationToken);
    }
}
```

**Track exception metrics:**

```csharp
public class MetricsExceptionAction : IRequestExceptionAction<IRequest, Exception>
{
    private readonly IMetricsService _metrics;

    public MetricsExceptionAction(IMetricsService metrics)
        => _metrics = metrics;

    public Task Execute(IRequest request, Exception exception, CancellationToken cancellationToken)
    {
        _metrics.IncrementCounter(
            "request_exceptions",
            tags: new Dictionary<string, string>
            {
                ["request_type"] = request.GetType().Name,
                ["exception_type"] = exception.GetType().Name
            });

        return Task.CompletedTask;
    }
}
```

---

## Exception Action Processor Strategy

Control when exception actions execute relative to exception handlers.

### Strategies

```csharp
public enum RequestExceptionActionProcessorStrategy
{
    // Actions only run if no handler marks the exception as handled
    ApplyForUnhandledExceptions,

    // Actions always run, regardless of whether a handler handles the exception
    ApplyForAllExceptions
}
```

### Configuration

```csharp
using MediateX.Processing;

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Default: Only for unhandled exceptions
    cfg.RequestExceptionActionProcessorStrategy =
        RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    // Alternative: For all exceptions
    cfg.RequestExceptionActionProcessorStrategy =
        RequestExceptionActionProcessorStrategy.ApplyForAllExceptions;
});
```

---

## Built-in Exception Behaviors

MediateX includes two pipeline behaviors for exception handling:

### RequestExceptionProcessorBehavior

Executes registered `IRequestExceptionHandler<,,>` handlers when exceptions occur.

**Automatically registered** when you call `AddMediateX()`.

### RequestExceptionActionProcessorBehavior

Executes registered `IRequestExceptionAction<,>` actions when exceptions occur.

**Automatically registered** when you call `AddMediateX()`.

---

## Complete Example

Here's a comprehensive example showing handlers and actions working together:

### Domain Exceptions

```csharp
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message) { }
}
```

### Request and Handler

```csharp
public record ProcessPaymentCommand(int OrderId, decimal Amount) : IRequest<PaymentResult>;

public class ProcessPaymentHandler : IRequestHandler<ProcessPaymentCommand, PaymentResult>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IOrderRepository _orderRepository;

    public ProcessPaymentHandler(IPaymentGateway paymentGateway, IOrderRepository orderRepository)
    {
        _paymentGateway = paymentGateway;
        _orderRepository = orderRepository;
    }

    public async Task<PaymentResult> Handle(
        ProcessPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Order {request.OrderId} not found");

        if (order.Amount != request.Amount)
        {
            throw new ValidationException("Payment amount does not match order amount");
        }

        return await _paymentGateway.ProcessAsync(order, cancellationToken);
    }
}
```

### Exception Handlers

```csharp
// Handle not found exceptions
public class NotFoundExceptionHandler
    : IRequestExceptionHandler<ProcessPaymentCommand, PaymentResult, NotFoundException>
{
    private readonly ILogger<NotFoundExceptionHandler> _logger;

    public NotFoundExceptionHandler(ILogger<NotFoundExceptionHandler> logger)
        => _logger = logger;

    public Task Handle(
        ProcessPaymentCommand request,
        NotFoundException exception,
        RequestExceptionHandlerState<PaymentResult> state,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Order not found");

        state.SetHandled(PaymentResult.Failed("Order not found"));

        return Task.CompletedTask;
    }
}

// Handle validation exceptions
public class ValidationExceptionHandler
    : IRequestExceptionHandler<ProcessPaymentCommand, PaymentResult, ValidationException>
{
    private readonly ILogger<ValidationExceptionHandler> _logger;

    public ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger)
        => _logger = logger;

    public Task Handle(
        ProcessPaymentCommand request,
        ValidationException exception,
        RequestExceptionHandlerState<PaymentResult> state,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Validation failed");

        state.SetHandled(PaymentResult.Failed(exception.Message));

        return Task.CompletedTask;
    }
}

// Generic handler for all domain exceptions
public class DomainExceptionHandler
    : IRequestExceptionHandler<ProcessPaymentCommand, PaymentResult, DomainException>
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
        => _logger = logger;

    public Task Handle(
        ProcessPaymentCommand request,
        DomainException exception,
        RequestExceptionHandlerState<PaymentResult> state,
        CancellationToken cancellationToken)
    {
        // Catch-all for any unhandled domain exceptions
        _logger.LogError(exception, "Domain exception occurred");

        state.SetHandled(PaymentResult.Failed("An error occurred processing your payment"));

        return Task.CompletedTask;
    }
}
```

### Exception Actions

```csharp
// Log all exceptions
public class LogExceptionAction : IRequestExceptionAction<ProcessPaymentCommand, Exception>
{
    private readonly ILogger<LogExceptionAction> _logger;

    public LogExceptionAction(ILogger<LogExceptionAction> logger)
        => _logger = logger;

    public Task Execute(
        ProcessPaymentCommand request,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Exception occurred processing payment for order {OrderId}",
            request.OrderId);

        return Task.CompletedTask;
    }
}

// Alert on payment gateway exceptions
public class PaymentGatewayExceptionAction
    : IRequestExceptionAction<ProcessPaymentCommand, PaymentGatewayException>
{
    private readonly IAlertService _alertService;

    public PaymentGatewayExceptionAction(IAlertService alertService)
        => _alertService = alertService;

    public Task Execute(
        ProcessPaymentCommand request,
        PaymentGatewayException exception,
        CancellationToken cancellationToken)
    {
        return _alertService.SendAlertAsync(
            "Payment gateway error",
            exception.Message,
            cancellationToken);
    }
}
```

---

## Registration

Exception handlers and actions are automatically discovered and registered when scanning assemblies:

```csharp
builder.Services.AddMediateX(cfg =>
{
    // All exception handlers and actions in this assembly will be registered
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

---

## Best Practices

### Design Guidelines

1. **Use specific exception types:** Create meaningful exception hierarchies for your domain
2. **Handle at the right level:** Handle exceptions in handlers for that specific request type
3. **Don't swallow exceptions silently:** Always log or track exceptions even if handled
4. **Return meaningful responses:** When handling exceptions, provide useful feedback
5. **Keep actions side-effect only:** Exception actions should not affect control flow

### Exception Handler Guidelines

1. **Be specific:** Create handlers for specific exception types rather than catching all exceptions
2. **Use hierarchy wisely:** Take advantage of exception inheritance for common handling
3. **Mark as handled appropriately:** Only call `SetHandled()` when you can provide a valid response
4. **Log before handling:** Always log the exception before marking it as handled
5. **Avoid business logic:** Exception handlers should focus on recovery, not business logic

### Exception Action Guidelines

1. **Keep them lightweight:** Actions should execute quickly
2. **Don't throw exceptions:** Actions should not throw exceptions themselves
3. **Use for observability:** Perfect for logging, metrics, and alerting
4. **Be idempotent:** Actions may be called multiple times for the same exception

### Performance Considerations

1. **Minimize handler complexity:** Keep exception handlers simple and fast
2. **Avoid async when possible:** Use synchronous operations if no I/O is needed
3. **Cache expensive resources:** Don't recreate services or connections per exception
4. **Consider handler order:** More specific handlers execute first

---

## Testing Exception Handling

### Testing Exception Handlers

```csharp
[Fact]
public async Task NotFoundExceptionHandler_ReturnsFailedResult()
{
    // Arrange
    var logger = Substitute.For<ILogger<NotFoundExceptionHandler>>();
    var handler = new NotFoundExceptionHandler(logger);
    var request = new ProcessPaymentCommand(OrderId: 123, Amount: 99.99m);
    var exception = new NotFoundException("Order 123 not found");
    var state = new RequestExceptionHandlerState<PaymentResult>();

    // Act
    await handler.Handle(request, exception, state, CancellationToken.None);

    // Assert
    Assert.True(state.Handled);
    Assert.False(state.Response.Success);
    Assert.Equal("Order not found", state.Response.Message);
}
```

### Testing Exception Actions

```csharp
[Fact]
public async Task LogExceptionAction_LogsException()
{
    // Arrange
    var logger = Substitute.For<ILogger<LogExceptionAction>>();
    var action = new LogExceptionAction(logger);
    var request = new ProcessPaymentCommand(OrderId: 123, Amount: 99.99m);
    var exception = new PaymentGatewayException("Gateway timeout");

    // Act
    await action.Execute(request, exception, CancellationToken.None);

    // Assert
    logger.Received(1).LogError(
        exception,
        Arg.Is<string>(s => s.Contains("order 123")),
        Arg.Any<object[]>());
}
```

### Integration Testing

```csharp
[Fact]
public async Task Send_HandlesNotFoundException()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddMediateX(cfg =>
    {
        cfg.RegisterServicesFromAssemblyContaining<ProcessPaymentHandler>();
    });
    // Add mocked dependencies
    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();

    // Act
    var result = await mediator.Send(new ProcessPaymentCommand(OrderId: 999, Amount: 99.99m));

    // Assert
    Assert.False(result.Success);
    Assert.Equal("Order not found", result.Message);
}
```

---

## Common Patterns

### Circuit Breaker Pattern

```csharp
public class CircuitBreakerExceptionHandler<TRequest, TResponse>
    : IRequestExceptionHandler<TRequest, TResponse, HttpRequestException>
    where TRequest : notnull
{
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly ILogger<CircuitBreakerExceptionHandler<TRequest, TResponse>> _logger;

    public async Task Handle(
        TRequest request,
        HttpRequestException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        var serviceName = typeof(TRequest).Name;

        _circuitBreaker.RecordFailure(serviceName);

        if (_circuitBreaker.IsOpen(serviceName))
        {
            _logger.LogWarning("Circuit breaker open for {Service}", serviceName);
            // Provide cached or default response
        }
    }
}
```

### Fallback Pattern

```csharp
public class FallbackExceptionHandler
    : IRequestExceptionHandler<GetWeatherQuery, WeatherData, HttpRequestException>
{
    private readonly ICache _cache;
    private readonly ILogger<FallbackExceptionHandler> _logger;

    public async Task Handle(
        GetWeatherQuery request,
        HttpRequestException exception,
        RequestExceptionHandlerState<WeatherData> state,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Weather service unavailable, using cached data");

        var cachedData = await _cache.GetAsync<WeatherData>(
            $"weather:{request.City}",
            cancellationToken);

        if (cachedData != null)
        {
            state.SetHandled(cachedData);
        }
    }
}
```

---

## Next Steps

- **[Streaming](./07-streaming.md)** - Work with streaming requests
- **[Configuration](./05-configuration.md)** - Configure exception handling strategies
- **[Pipeline Behaviors](./04-behaviors.md)** - Add cross-cutting concerns
