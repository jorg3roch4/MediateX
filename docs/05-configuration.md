# Configuration

This guide covers all configuration options available in MediateX, from basic setup to advanced customization.

---

## Basic Setup

### Minimal Configuration

The simplest way to configure MediateX:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

This registers all handlers, notifications, and behaviors from the assembly containing `Program`.

### Multiple Assemblies

Register handlers from multiple assemblies:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CoreHandlers).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(ApiHandlers).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(BackgroundHandlers).Assembly);
});

// Or use the params overload
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(CoreHandlers).Assembly,
        typeof(ApiHandlers).Assembly,
        typeof(BackgroundHandlers).Assembly
    );
});
```

---

## Service Lifetime

Configure the default lifetime for all MediateX services:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Default: Transient (new instance per request)
    cfg.Lifetime = ServiceLifetime.Transient;

    // Scoped: One instance per HTTP request/scope
    cfg.Lifetime = ServiceLifetime.Scoped;

    // Singleton: Single instance for application lifetime
    cfg.Lifetime = ServiceLifetime.Singleton;
});
```

**Recommendations:**
- **Transient** (default): Best for most scenarios, especially with async handlers
- **Scoped**: Use when handlers need to share state within a request
- **Singleton**: Rarely needed; use only for stateless handlers with no dependencies

---

## Type Filtering

Filter which types get registered using a custom type evaluator:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Only register handlers in specific namespace
    cfg.TypeEvaluator = type =>
        type.Namespace?.StartsWith("MyApp.Handlers") ?? false;
});
```

### Common Filtering Scenarios

**Exclude test handlers:**

```csharp
cfg.TypeEvaluator = type =>
    !type.Namespace?.Contains("Tests") ?? true;
```

**Include only specific assemblies:**

```csharp
cfg.TypeEvaluator = type =>
{
    var assembly = type.Assembly.GetName().Name;
    return assembly == "MyApp.Core" || assembly == "MyApp.Infrastructure";
};
```

**Exclude handlers with specific attribute:**

```csharp
cfg.TypeEvaluator = type =>
    !type.GetCustomAttributes(typeof(ExcludeFromMediatorAttribute), false).Any();
```

---

## Notification Publishers

Configure how notifications are published to multiple handlers.

### Built-in Publishers

**ForeachAwaitPublisher (Default):**

Executes handlers sequentially, one at a time:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new ForeachAwaitPublisher();
});
```

**TaskWhenAllPublisher:**

Executes all handlers in parallel:

```csharp
using MediateX.Publishing;

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new TaskWhenAllPublisher();
});
```

### Publisher by Type

Register publisher by type (resolved from DI):

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
});
```

### Custom Publisher

Use a custom publisher implementation:

```csharp
using MediateX.Core;

public class CustomPublisher : INotificationPublisher
{
    public async Task Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        // Custom publishing logic
        foreach (var handler in handlerExecutors)
        {
            await handler.HandlerCallback(notification, cancellationToken);
        }
    }
}

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new CustomPublisher();
});
```

---

## Pipeline Behaviors

Add cross-cutting concerns to the request pipeline.

### Open Generic Behaviors

Apply to all requests:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Register open generic behaviors
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
```

### Closed Generic Behaviors

Apply to specific request types:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Register for specific request/response types
    cfg.AddBehavior<IPipelineBehavior<CreateOrderCommand, int>, OrderValidationBehavior>();

    // Or let it infer the interface
    cfg.AddBehavior<OrderValidationBehavior>();
});
```

### Behavior Lifetime

Specify service lifetime per behavior:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Transient (default)
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Transient);

    // Scoped (for behaviors with DbContext)
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>), ServiceLifetime.Scoped);

    // Singleton
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>), ServiceLifetime.Singleton);
});
```

### Multiple Open Behaviors

Register multiple behaviors at once:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    var behaviorTypes = new[]
    {
        typeof(LoggingBehavior<,>),
        typeof(ValidationBehavior<,>),
        typeof(PerformanceBehavior<,>)
    };

    cfg.AddOpenBehaviors(behaviorTypes, ServiceLifetime.Transient);
});
```

### Execution Order

Behaviors execute in **reverse order** of registration:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));      // Executes 3rd (outermost)
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));   // Executes 2nd
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));  // Executes 1st (innermost)
});
```

---

## Stream Behaviors

Configure behaviors for streaming requests.

### Register Stream Behaviors

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Open generic stream behavior
    cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior<,>));

    // Closed generic stream behavior
    cfg.AddStreamBehavior<StreamValidationBehavior>();

    // With lifetime
    cfg.AddOpenStreamBehavior(typeof(StreamCachingBehavior<,>), ServiceLifetime.Scoped);
});
```

---

## Request Pre/Post Processors

Add processing before and after request handlers.

### Pre-Processors

Execute before the handler:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Open generic pre-processor
    cfg.AddOpenRequestPreProcessor(typeof(LoggingPreProcessor<>));

    // Closed generic pre-processor
    cfg.AddRequestPreProcessor<AuditPreProcessor>();

    // Auto-register all pre-processors from assembly
    cfg.AutoRegisterRequestProcessors = true;
});
```

**Example Pre-Processor:**

```csharp
public class LoggingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<LoggingPreProcessor<TRequest>> _logger;

    public LoggingPreProcessor(ILogger<LoggingPreProcessor<TRequest>> logger)
        => _logger = logger;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing {RequestName}", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}
```

### Post-Processors

Execute after the handler:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Open generic post-processor
    cfg.AddOpenRequestPostProcessor(typeof(LoggingPostProcessor<,>));

    // Closed generic post-processor
    cfg.AddRequestPostProcessor<CacheInvalidationPostProcessor>();
});
```

**Example Post-Processor:**

```csharp
public class LoggingPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingPostProcessor<TRequest, TResponse>> _logger;

    public LoggingPostProcessor(ILogger<LoggingPostProcessor<TRequest, TResponse>> logger)
        => _logger = logger;

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processed {RequestName}", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}
```

---

## Exception Handling

Configure exception handling behaviors.

### Request Exception Action Strategy

Control when exception actions execute:

```csharp
using MediateX.Processing;

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Only execute actions for unhandled exceptions (default)
    cfg.RequestExceptionActionProcessorStrategy =
        RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    // Execute actions for all exceptions
    cfg.RequestExceptionActionProcessorStrategy =
        RequestExceptionActionProcessorStrategy.ApplyForAllExceptions;
});
```

**RequestExceptionActionProcessorStrategy values:**
- `ApplyForUnhandledExceptions` (default): Execute actions only if no exception handler handles the exception
- `ApplyForAllExceptions`: Execute actions for every exception, even if handled

---

## Custom Mediator Implementation

Replace the default mediator with a custom implementation:

```csharp
public class CustomMediator : IMediator
{
    // Custom implementation
}

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.MediatorImplementationType = typeof(CustomMediator);
});
```

---

## Generic Handler Registration

Control registration of open generic handlers.

### Enable Generic Handlers

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Enable registration of open generic handlers
    cfg.RegisterGenericHandlers = true;

    // Configure limits
    cfg.MaxGenericTypeParameters = 10;      // Max type parameters per handler
    cfg.MaxTypesClosing = 100;              // Max types that can close a generic constraint
    cfg.MaxGenericTypeRegistrations = 125000; // Max total generic registrations
    cfg.RegistrationTimeout = 15000;        // Timeout in milliseconds
});
```

**Generic handler example:**

```csharp
public class GenericHandler<T> : IRequestHandler<GenericRequest<T>, T>
    where T : class
{
    public Task<T> Handle(GenericRequest<T> request, CancellationToken cancellationToken)
    {
        // Generic handling logic
        return Task.FromResult(request.Data);
    }
}
```

### Safety Limits

These limits prevent excessive registration times:

- **MaxGenericTypeParameters**: Limits complexity of generic types
- **MaxTypesClosing**: Prevents combinatorial explosion
- **MaxGenericTypeRegistrations**: Caps total registrations
- **RegistrationTimeout**: Fails fast if registration takes too long

---

## Built-in Behaviors Configuration (v3.1.0+)

MediateX includes ready-to-use behaviors. The `Result<T>` types they use are in `MediateX.Contracts` namespace.

### Logging

```csharp
cfg.AddLoggingBehavior(); // Use defaults
cfg.AddLoggingBehavior(opt =>
{
    opt.LogRequestStart = true;
    opt.LogRequestFinish = true;
    opt.LogRequestException = true;
    opt.SlowRequestThresholdMs = 500;
});
```

### Validation

```csharp
cfg.AddValidationBehavior();       // Throws ValidationException
cfg.AddValidationResultBehavior(); // Returns Result<T>.Failure
cfg.AddRequestValidator<MyValidator>(); // Register specific validator
```

### Retry

```csharp
cfg.AddRetryBehavior();       // Retries on exception
cfg.AddRetryResultBehavior(); // Retries on Result<T>.Failure
cfg.AddRetryBehavior(opt =>
{
    opt.MaxRetryAttempts = 3;
    opt.BaseDelay = TimeSpan.FromMilliseconds(100);
    opt.MaxDelay = TimeSpan.FromSeconds(10);
    opt.UseJitter = true;
    opt.ShouldRetryException = ex => ex is HttpRequestException;
});
```

### Timeout

```csharp
cfg.AddTimeoutBehavior();       // Throws TimeoutException
cfg.AddTimeoutResultBehavior(); // Returns Result<T>.Failure
cfg.AddTimeoutBehavior(opt =>
{
    opt.DefaultTimeout = TimeSpan.FromSeconds(30);
    opt.SetTimeout<SlowQuery>(TimeSpan.FromMinutes(2));
});
```

---

## Complete Configuration Example

A comprehensive configuration with common patterns:

```csharp
using MediateX.Publishing;
using MediateX.Processing;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediateX(cfg =>
{
    // Register assemblies
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RegisterServicesFromAssembly(typeof(CoreHandlers).Assembly);

    // Service lifetime
    cfg.Lifetime = ServiceLifetime.Scoped;

    // Type filtering
    cfg.TypeEvaluator = type =>
        !type.Namespace?.Contains("Tests") ?? true;

    // Notification publishing strategy
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);

    // Exception handling
    cfg.RequestExceptionActionProcessorStrategy =
        RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    // Built-in behaviors (v3.1.0+)
    cfg.AddLoggingBehavior(opt => opt.SlowRequestThresholdMs = 500);
    cfg.AddValidationBehavior();
    cfg.AddRetryBehavior(opt => opt.MaxRetryAttempts = 3);
    cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(30));

    // Custom pipeline behaviors (outer to inner)
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>), ServiceLifetime.Scoped);
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>), ServiceLifetime.Scoped);

    // Stream behaviors
    cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior<,>));

    // Request processors
    cfg.AutoRegisterRequestProcessors = true;
    cfg.AddOpenRequestPreProcessor(typeof(AuditPreProcessor<>));
    cfg.AddOpenRequestPostProcessor(typeof(CacheInvalidationPostProcessor<,>));

    // Generic handlers
    cfg.RegisterGenericHandlers = false;
});

var app = builder.Build();
app.Run();
```

---

## Configuration Best Practices

### Assembly Registration

1. **Register early:** Call `AddMediateX()` before other service registrations that depend on handlers
2. **Multiple assemblies:** Register all assemblies containing handlers
3. **Type filtering:** Use `TypeEvaluator` to exclude test assemblies or internal handlers

**Note on assembly scanning:** MediateX safely handles assemblies containing types that cannot be loaded (e.g., F# assemblies with `inref`/`outref` types, or assemblies with missing dependencies). These types are silently skipped during registration, preventing `ReflectionTypeLoadException` crashes.

### Lifetime Management

1. **Default to Transient:** Works for most scenarios
2. **Use Scoped for DbContext:** When behaviors or handlers use Entity Framework
3. **Avoid Singleton:** Unless handlers are truly stateless and have no dependencies

### Behavior Order

1. **Outer to inner:** Register behaviors in the order they should wrap the handler
2. **Logging first:** Put logging behaviors outermost to capture everything
3. **Validation early:** Validate before expensive operations like transactions
4. **Transactions innermost:** Keep transaction scope as small as possible

### Performance

1. **Profile registration time:** Monitor startup time when using generic handlers
2. **Limit generic types:** Set reasonable limits on generic handler registration
3. **Use open generics:** More efficient than registering many closed generics

---

## Environment-Specific Configuration

Configure differently per environment:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    if (builder.Environment.IsDevelopment())
    {
        // Detailed logging in development
        cfg.AddOpenBehavior(typeof(DetailedLoggingBehavior<,>));
    }
    else
    {
        // Performance-optimized logging in production
        cfg.AddOpenBehavior(typeof(MinimalLoggingBehavior<,>));
    }

    if (builder.Environment.IsProduction())
    {
        // Enable caching in production
        cfg.AddOpenBehavior(typeof(CachingBehavior<,>), ServiceLifetime.Scoped);
    }
});
```

---

## Troubleshooting

### Handlers Not Registered

**Problem:** Handlers are not being found.

**Solutions:**
- Ensure the assembly containing handlers is registered
- Check that handlers are public and not abstract
- Verify `TypeEvaluator` isn't filtering out handlers
- Confirm handlers implement correct interfaces

### Behaviors Not Executing

**Problem:** Behaviors are registered but not running.

**Solutions:**
- Verify behaviors are registered before calling `Build()`
- Check that open generic behaviors have `<,>` syntax
- Ensure behavior implements `IPipelineBehavior<,>`
- Confirm service lifetime matches usage pattern

### Registration Timeout

**Problem:** `AddMediateX()` takes too long or times out.

**Solutions:**
- Disable generic handlers: `cfg.RegisterGenericHandlers = false`
- Reduce limits: `cfg.MaxGenericTypeRegistrations = 50000`
- Filter types: `cfg.TypeEvaluator = ...`
- Increase timeout: `cfg.RegistrationTimeout = 30000`

---

## Next Steps

- **[Exception Handling](./06-exception-handling.md)** - Handle exceptions gracefully in the pipeline
- **[Streaming](./07-streaming.md)** - Work with streaming requests
- **[Getting Started](./01-getting-started.md)** - Review the basics
