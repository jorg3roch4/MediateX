# Pipeline Behaviors

Behaviors wrap around request handlers, letting you execute logic before and after the handler runs. Use them for cross-cutting concerns like logging, validation, caching, and transactions.

---

## Core Concepts

### IPipelineBehavior Interface

Behaviors implement `IPipelineBehavior<TRequest, TResponse>`:

```csharp
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

### RequestHandlerDelegate

The `next` delegate represents the next step in the pipeline:

```csharp
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken t = default);
```

Calling `await next(cancellationToken)` invokes the next behavior or the final handler.

---

## Execution Order

Behaviors execute in **reverse order** of registration:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddBehavior<LoggingBehavior<,>>();        // Executes 3rd (outermost)
    cfg.AddBehavior<ValidationBehavior<,>>();     // Executes 2nd
    cfg.AddBehavior<TransactionBehavior<,>>();    // Executes 1st (innermost)
});
```

**Execution flow:**
```
Request
  -> LoggingBehavior (start)
    -> ValidationBehavior (start)
      -> TransactionBehavior (start)
        -> Handler
      -> TransactionBehavior (end)
    -> ValidationBehavior (end)
  -> LoggingBehavior (end)
Response
```

---

## Common Use Cases

### 1. Logging

Log request/response details and execution time:

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### 2. Validation

Validate requests before they reach the handler:

```csharp
using FluentValidation;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
```

### 3. Caching

Cache responses to avoid redundant processing:

```csharp
public interface ICacheableRequest
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        IDistributedCache cache,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only cache requests that implement ICacheableRequest
        if (request is not ICacheableRequest cacheableRequest)
        {
            return await next(cancellationToken);
        }

        var cacheKey = cacheableRequest.CacheKey;

        // Try to get from cache
        var cachedResponse = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogInformation("Cache hit for {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<TResponse>(cachedResponse)!;
        }

        _logger.LogInformation("Cache miss for {CacheKey}", cacheKey);

        // Execute handler
        var response = await next(cancellationToken);

        // Store in cache
        var serializedResponse = JsonSerializer.Serialize(response);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheableRequest.CacheDuration
        };

        await _cache.SetStringAsync(cacheKey, serializedResponse, options, cancellationToken);

        return response;
    }
}

// Usage example
public record GetProductQuery(int ProductId) : IRequest<Product>, ICacheableRequest
{
    public string CacheKey => $"product:{ProductId}";
    public TimeSpan CacheDuration => TimeSpan.FromMinutes(5);
}
```

### 4. Transaction Management

Wrap handlers in database transactions:

```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        AppDbContext dbContext,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip if already in a transaction
        if (_dbContext.Database.CurrentTransaction != null)
        {
            return await next(cancellationToken);
        }

        _logger.LogInformation("Beginning transaction for {RequestName}", typeof(TRequest).Name);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Committed transaction for {RequestName}", typeof(TRequest).Name);

            return response;
        }
        catch (Exception)
        {
            _logger.LogError("Rolling back transaction for {RequestName}", typeof(TRequest).Name);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

### 5. Performance Monitoring

Track performance metrics:

```csharp
public class PerformanceMonitoringBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> _logger;
    private readonly IMetricsService _metrics;

    public PerformanceMonitoringBehavior(
        ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> logger,
        IMetricsService metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);

            stopwatch.Stop();
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            // Record metrics
            _metrics.RecordRequestDuration(requestName, elapsedMilliseconds);

            // Log slow requests
            if (elapsedMilliseconds > 1000)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestName} took {ElapsedMilliseconds}ms",
                    requestName,
                    elapsedMilliseconds);
            }

            return response;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _metrics.RecordRequestFailure(requestName);
            throw;
        }
    }
}
```

---

## Registration Methods

### AddBehavior (Closed Generic)

Register a behavior for specific request/response types:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Register for specific types
    cfg.AddBehavior<IPipelineBehavior<CreateOrderCommand, int>, OrderValidationBehavior>();

    // Or let it infer the interface
    cfg.AddBehavior<OrderValidationBehavior>();
});

public class OrderValidationBehavior : IPipelineBehavior<CreateOrderCommand, int>
{
    public async Task<int> Handle(
        CreateOrderCommand request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        // Specific validation for CreateOrderCommand
        if (request.Total < 0)
            throw new ValidationException("Total must be positive");

        return await next(cancellationToken);
    }
}
```

### AddOpenBehavior (Open Generic)

Register a behavior for all requests (most common):

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Register open generic behavior
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
});
```

### Specify Service Lifetime

Control the lifetime of behaviors:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Transient (default)
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Transient);

    // Scoped (useful for behaviors using DbContext)
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>), ServiceLifetime.Scoped);

    // Singleton (use with caution)
    cfg.AddOpenBehavior(typeof(MetricsBehavior<,>), ServiceLifetime.Singleton);
});
```

### Nested Generic Response Types

MediateX supports behaviors with nested generic response types. This is useful when your requests return wrapped types like `Result<T>`, `ApiResponse<T>`, or `List<T>`:

```csharp
// A common Result wrapper type
public class Result<T>
{
    public T? Value { get; set; }
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }

    public static Result<T> Success(T value) => new() { Value = value, IsSuccess = true };
    public static Result<T> Failure(string error) => new() { Error = error, IsSuccess = false };
}

// Request that returns Result<T>
public record GetUserQuery(int UserId) : IRequest<Result<UserDto>>;

// Behavior that handles all Result<T> responses
public class ResultUnwrappingBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : IRequest<Result<TValue>>
{
    private readonly ILogger<ResultUnwrappingBehavior<TRequest, TValue>> _logger;

    public ResultUnwrappingBehavior(ILogger<ResultUnwrappingBehavior<TRequest, TValue>> logger)
        => _logger = logger;

    public async Task<Result<TValue>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await next(cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Request {RequestName} failed: {Error}",
                    typeof(TRequest).Name, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request {RequestName} threw an exception", typeof(TRequest).Name);
            return Result<TValue>.Failure(ex.Message);
        }
    }
}

// Registration - MediateX automatically infers nested type parameters
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(ResultUnwrappingBehavior<,>));
});
```

**How it works:** When you register `ResultUnwrappingBehavior<,>`, MediateX uses type unification to automatically match it against all requests returning `Result<T>`. For `GetUserQuery : IRequest<Result<UserDto>>`, it creates `ResultUnwrappingBehavior<GetUserQuery, UserDto>`.

**Supported nesting patterns:**

```csharp
// Single level nesting
IPipelineBehavior<TRequest, Result<T>>
IPipelineBehavior<TRequest, List<T>>
IPipelineBehavior<TRequest, Option<T>>

// Deep nesting (also supported)
IPipelineBehavior<TRequest, Result<List<T>>>
IPipelineBehavior<TRequest, ApiResponse<Dictionary<TKey, List<TValue>>>>
```

---

## Chaining Multiple Behaviors

Behaviors chain together automatically:

```csharp
public class RequestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RequestLoggingBehavior<TRequest, TResponse>> _logger;

    public RequestLoggingBehavior(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Request: {Request}", JsonSerializer.Serialize(request));

        var response = await next(cancellationToken);

        _logger.LogInformation("Response: {Response}", JsonSerializer.Serialize(response));

        return response;
    }
}

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation("Execution time: {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

        return response;
    }
}

// Register both
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));  // Outer
    cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));     // Inner
});
```

---

## Conditional Behavior Execution

Execute behavior logic conditionally:

```csharp
// Marker interface for requests that need authorization
public interface IAuthorizedRequest
{
    string[] RequiredRoles { get; }
}

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        ICurrentUserService currentUser,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only apply authorization to requests that need it
        if (request is not IAuthorizedRequest authorizedRequest)
        {
            return await next(cancellationToken);
        }

        var user = _currentUser.User;
        if (user == null)
        {
            throw new UnauthorizedException("User is not authenticated");
        }

        var hasRequiredRole = authorizedRequest.RequiredRoles
            .Any(role => user.IsInRole(role));

        if (!hasRequiredRole)
        {
            _logger.LogWarning(
                "User {UserId} attempted to access {RequestName} without required role",
                user.Id,
                typeof(TRequest).Name);

            throw new ForbiddenException("User does not have required permissions");
        }

        return await next(cancellationToken);
    }
}

// Usage
public record DeleteUserCommand(int UserId) : IRequest, IAuthorizedRequest
{
    public string[] RequiredRoles => new[] { "Admin", "SuperUser" };
}
```

---

## Advanced Patterns

### Retry Logic

```csharp
using Polly;

public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;

    public RetryBehavior(ILogger<RetryBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} for {RequestName} after {DelaySeconds}s",
                        retryCount,
                        typeof(TRequest).Name,
                        timeSpan.TotalSeconds);
                });

        return await retryPolicy.ExecuteAsync(async () => await next(cancellationToken));
    }
}
```

### Request Transformation

```csharp
public class RequestEnricherBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;

    public RequestEnricherBehavior(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Enrich request with current user info if needed
        if (request is IAuditableRequest auditableRequest)
        {
            auditableRequest.UserId = _currentUser.UserId;
            auditableRequest.Timestamp = DateTime.UtcNow;
        }

        return next(cancellationToken);
    }
}
```

---

## Best Practices

### Design Guidelines

1. **Keep behaviors focused:** Each behavior should have a single responsibility
2. **Make behaviors generic:** Use open generics to apply to all requests
3. **Order matters:** Register behaviors in the correct order (outer to inner)
4. **Don't modify the request:** Requests should be immutable; read-only access is preferred
5. **Always await next:** Call `await next(cancellationToken)` exactly once per behavior
6. **Handle exceptions carefully:** Decide whether to catch, log, or let exceptions bubble up

### Performance Considerations

1. **Minimize overhead:** Keep behavior logic lightweight
2. **Use async properly:** Don't use `.Result` or `.Wait()`
3. **Cache expensive operations:** Don't recreate services or configurations per request
4. **Consider lifetime:** Use appropriate service lifetime (Transient, Scoped, Singleton)

### Testing

Test behaviors in isolation:

```csharp
[Fact]
public async Task LoggingBehavior_LogsRequestAndResponse()
{
    // Arrange
    var logger = Substitute.For<ILogger<LoggingBehavior<TestRequest, TestResponse>>>();
    var behavior = new LoggingBehavior<TestRequest, TestResponse>(logger);
    var request = new TestRequest();
    var expectedResponse = new TestResponse();

    RequestHandlerDelegate<TestResponse> next = (ct) => Task.FromResult(expectedResponse);

    // Act
    var response = await behavior.Handle(request, next, CancellationToken.None);

    // Assert
    Assert.Equal(expectedResponse, response);
    logger.Received().LogInformation(Arg.Any<string>(), Arg.Any<object[]>());
}
```

---

## Common Pitfalls

1. **Not calling next:** Always call `await next(cancellationToken)` or the handler won't execute

2. **Calling next multiple times:** Only call `next` once per behavior

3. **Ignoring cancellation token:** Always pass the cancellation token to async operations

4. **Wrong registration order:** Remember behaviors execute in reverse order of registration

5. **Heavy synchronous operations:** Keep behaviors async and non-blocking

6. **Stateful behaviors:** Behaviors should be stateless; don't store request-specific data in fields

---

## Open Generic vs Closed Generic

| Feature | Open Generic | Closed Generic |
|---------|-------------|----------------|
| Applies to | All requests | Specific request types |
| Registration | `AddOpenBehavior(typeof(Behavior<,>))` | `AddBehavior<SpecificBehavior>()` |
| Use case | Cross-cutting concerns | Request-specific logic |
| Example | Logging, validation | Order-specific validation |

---

## Built-in Behaviors (v3.1.0+)

MediateX includes ready-to-use behaviors for common cross-cutting concerns. These behaviors integrate with the `Result<T>` pattern for functional error handling.

### Result<T> Pattern

The `Result<T>` types are in the `MediateX.Contracts` namespace:

```csharp
using MediateX.Contracts; // Result<T>, Result, Error, IResultRequest<T>

public record GetUserQuery(int Id) : IRequest<Result<User>>;

public class GetUserHandler : IRequestHandler<GetUserQuery, Result<User>>
{
    public async Task<Result<User>> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await _repo.FindAsync(request.Id, ct);
        return user is null
            ? Result<User>.Failure("NotFound", "User not found")
            : Result<User>.Success(user);
    }
}
```

**Result<T> API:**
- `Result<T>.Success(value)` - Create success result
- `Result<T>.Failure(code, message)` - Create failure result
- `result.IsSuccess` / `result.IsFailure` - Check state
- `result.Value` - Get value (throws if failure)
- `result.Error` - Get error (null if success)
- `result.Match(onSuccess, onFailure)` - Pattern match
- `result.Map(func)` - Transform value
- `result.Bind(func)` - Chain operations

### LoggingBehavior

Automatic request logging with performance metrics:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddLoggingBehavior(opt =>
    {
        opt.LogRequestStart = true;      // Log when request starts
        opt.LogRequestFinish = true;     // Log when request completes
        opt.LogRequestException = true;  // Log exceptions
        opt.SlowRequestThresholdMs = 500; // Warn for slow requests
    });
});
```

### ValidationBehavior

Automatic request validation using `IRequestValidator<T>`:

```csharp
using MediateX.Contracts;

builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddValidationBehavior(); // Throws ValidationException on failure
    // OR
    cfg.AddValidationResultBehavior(); // Returns Result<T>.Failure on failure
});

// Define validator
public class CreateUserValidator : IRequestValidator<CreateUserCommand>
{
    public ValueTask<ValidationResult> ValidateAsync(CreateUserCommand cmd, CancellationToken ct)
        => ValueTask.FromResult(
            new ValidationResultBuilder()
                .RequireNotEmpty(cmd.Name, "Name")
                .RequireNotEmpty(cmd.Email, "Email")
                .Build());
}
```

### RetryBehavior

Automatic retries with exponential backoff:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddRetryBehavior(opt =>
    {
        opt.MaxRetryAttempts = 3;
        opt.BaseDelay = TimeSpan.FromMilliseconds(100);
        opt.MaxDelay = TimeSpan.FromSeconds(10);
        opt.UseJitter = true; // Prevents thundering herd
        opt.ShouldRetryException = ex => ex is HttpRequestException;
    });
    // OR for Result<T> requests:
    cfg.AddRetryResultBehavior(opt =>
    {
        opt.ShouldRetryResultError = error => error.Code == "Transient";
    });
});
```

### TimeoutBehavior

Request timeout enforcement:

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddTimeoutBehavior(opt =>
    {
        opt.DefaultTimeout = TimeSpan.FromSeconds(30);
        opt.SetTimeout<SlowQuery>(TimeSpan.FromMinutes(2)); // Per-request
    });
    // OR for Result<T> requests (returns failure instead of throwing):
    cfg.AddTimeoutResultBehavior();
});

// Or implement IHasTimeout on the request:
public record SlowQuery : IRequest<Data>, IHasTimeout
{
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
}
```

---

## Next Steps

- **[Configuration](./05-configuration.md)** - Advanced configuration options
- **[Exception Handling](./06-exception-handling.md)** - Handle exceptions in the pipeline
- **[Streaming](./07-streaming.md)** - Work with streaming requests and behaviors
