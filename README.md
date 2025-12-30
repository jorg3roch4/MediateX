![MediateX Logo](https://raw.githubusercontent.com/jorg3roch4/MediateX/main/assets/mediatex-brand.png)

# MediateX

**Mediator Pattern for .NET 10+**

[![NuGet](https://img.shields.io/nuget/v/MediateX.svg?style=flat-square)](https://www.nuget.org/packages/MediateX) [![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg?style=flat-square)](https://github.com/jorg3roch4/MediateX/blob/main/LICENSE) [![C#](https://img.shields.io/badge/C%23-14-239120.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/) [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square)](https://dotnet.microsoft.com/)

**MediateX** is a mediator library for .NET 10+.

I dropped support for older .NET versions to simplify the codebase and take full advantage of .NET 10 and C# 14 features.

---

## Support the Project

MediateX is free and will always be free. But maintaining it takes real effort: keeping up with .NET releases, fixing bugs, adding features, writing docs, and testing across DI containers.

If this library saves you time or helps your projects, consider supporting its development. Every contribution helps me dedicate more time to making MediateX better.

**No pressure.** A GitHub star or sharing with your team helps too.

[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/jorg3roch4) [![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/jorg3roch4)

---

## Why MediateX?

### The Problem

Every .NET application needs cross-cutting concerns: validation, logging, retries, timeouts. Without a mediator, you end up with:

```csharp
// Without MediateX: 40+ lines of boilerplate PER handler
public class CreateUserHandler
{
    public async Task<User> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        // Validation (duplicated in every handler)
        if (string.IsNullOrEmpty(cmd.Name)) throw new ValidationException("Name required");
        if (string.IsNullOrEmpty(cmd.Email)) throw new ValidationException("Email required");

        // Logging (15-20 lines per handler)
        _logger.LogInformation("Creating user {Email}", cmd.Email);
        var stopwatch = Stopwatch.StartNew();

        // Retry logic (complex, error-prone)
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var user = await _repo.Create(cmd);
                _logger.LogInformation("Created in {Ms}ms", stopwatch.ElapsedMilliseconds);
                return user;
            }
            catch (Exception ex) when (attempt < 3)
            {
                await Task.Delay(200 * attempt, ct);
            }
        }
        throw new Exception("Failed after 3 attempts");
    }
}
```

### The Solution

```csharp
// With MediateX: Clean handler, behaviors handle the rest
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<User>>
{
    public async Task<Result<User>> Handle(CreateUserCommand cmd, CancellationToken ct)
        => Result<User>.Success(await _repo.Create(cmd));
}

// One-time configuration - applies to ALL handlers
services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddTimeoutBehavior();      // Timeout enforcement
    cfg.AddRetryBehavior();        // Automatic retries
    cfg.AddLoggingBehavior();      // Zero-allocation logging
    cfg.AddValidationBehavior();   // Automatic validation
});
```

### Production-Ready Behaviors

MediateX includes **4 battle-tested behaviors** that would take weeks to build yourself:

| Behavior | What It Does | Lines Saved |
|----------|--------------|-------------|
| **ValidationBehavior** | Automatic validation before handler execution | ~10-15 per handler |
| **LoggingBehavior** | Zero-allocation logging with slow request detection | ~15-20 per handler |
| **RetryBehavior** | Exponential backoff with jitter (prevents thundering herd) | ~15-25 per handler |
| **TimeoutBehavior** | Request timeout enforcement with per-request config | ~10-15 per handler |

In a typical application with 50 handlers, that's **2,000-3,500 lines of code you don't have to write or maintain**.

### Result&lt;T&gt; Pattern

Exceptions are slow (~1000x overhead) and hide error paths. MediateX includes a functional `Result<T>` type:

```csharp
// Errors are explicit in the type signature
public async Task<Result<User>> Handle(CreateUserCommand cmd, CancellationToken ct)
{
    if (await _repo.EmailExists(cmd.Email))
        return Result<User>.Failure("DuplicateEmail", "Email already exists");

    return Result<User>.Success(await _repo.Create(cmd));
}

// No try/catch, pattern matching instead
var result = await mediator.Send(command);
return result.Match(
    onSuccess: user => Ok(user),
    onFailure: error => BadRequest(error.Message)
);
```

### Why Not MediatR?

MediateX was forked from MediatR 12.5.0 and improved:

| Aspect | MediatR | MediateX |
|--------|---------|----------|
| Target | .NET 6+ (legacy support) | .NET 10 only (optimized) |
| Behaviors | None included | 4 production-ready behaviors |
| Result&lt;T&gt; | Not included | Full functional error handling |
| Validation | Not included | Integrated system |
| Logging | Not included | Zero-allocation with LoggerMessage |
| Retries | Not included | Exponential backoff + jitter |
| Timeouts | Not included | Per-request configuration |
| Nested Generics | Bug ([#1051](https://github.com/jbogard/MediatR/issues/1051)) | Fixed with TypeUnifier |
| Handler Duplication | Bug ([#1118](https://github.com/jbogard/MediatR/issues/1118)) | Fixed |
| F# Compatibility | Crashes on inref/outref | Safe handling |
| Tests | Legacy | 263 tests (v3.1.0) |

### Performance

- **Zero-allocation logging** using `LoggerMessage` source generators
- **Handler caching** with static `ConcurrentDictionary`
- **Modern C# 14** features: collection expressions, pattern matching, records
- **No legacy overhead** - only .NET 10, no compatibility shims

### DI Container Support

Works with **9 DI containers** out of the box:

- Microsoft.Extensions.DependencyInjection (built-in)
- Autofac, DryIoc, Lamar, LightInject
- SimpleInjector, Stashbox, Castle Windsor
- Any container implementing `IServiceProvider`

### Quality Assurance

- **263 unit tests** covering all scenarios
- **TreatWarningsAsErrors** enabled
- **Nullable reference types** enabled
- **7 documentation guides** (500+ KB)
- **10 sample projects** for different DI containers

---

## What's New in 3.1.0

Version 3.1.0 adds pipeline behaviors for common cross-cutting concerns.

### Result&lt;T&gt; - Functional Error Handling

Exceptions as control flow are slow (~1000x overhead) and hide error paths. `Result<T>` makes errors explicit in the type signature.

```csharp
// Errors are explicit in the return type
public async Task<Result<User>> Handle(CreateUserCommand cmd, CancellationToken ct)
{
    if (await _repo.EmailExists(cmd.Email))
        return Result<User>.Failure("DuplicateEmail", "Email already exists");

    return Result<User>.Success(await _repo.Create(cmd));
}

// Pattern matching instead of try/catch
var result = await mediator.Send(command);
return result.Match(
    onSuccess: user => Ok(user),
    onFailure: error => BadRequest(error.Message)
);
```

### ValidationBehavior - Clean Handlers

Validation logic clutters handlers and gets duplicated. The pipeline validates automatically before the handler runs.

```csharp
// Validator is separate and reusable
public class CreateUserValidator : IRequestValidator<CreateUserCommand>
{
    public ValueTask<ValidationResult> ValidateAsync(CreateUserCommand cmd, CancellationToken ct)
        => ValueTask.FromResult(
            new ValidationResultBuilder()
                .RequireNotEmpty(cmd.Name, "Name")
                .RequireNotEmpty(cmd.Email, "Email")
                .Build());
}

// Handler stays clean - no validation code
public class CreateUserHandler : IRequestHandler<CreateUserCommand, User>
{
    public async Task<User> Handle(CreateUserCommand cmd, CancellationToken ct)
        => await _repo.Create(cmd);
}
```

### LoggingBehavior - Automatic Observability

Manual logging adds 15-20 lines per handler and is inconsistent. The behavior logs all requests automatically with zero-allocation performance.

```csharp
// Just enable it - logs are automatic
cfg.AddLoggingBehavior(opt => opt.SlowRequestThresholdMs = 500);

// Output:
// [INF] Handling CreateUserCommand
// [INF] Handled CreateUserCommand in 45ms
// [WRN] Handled CreateUserCommand in 650ms (exceeded threshold)
```

### RetryBehavior - Resilient Operations

Retry logic is complex and error-prone. The behavior handles retries with exponential backoff and jitter automatically.

```csharp
cfg.AddRetryBehavior(opt =>
{
    opt.MaxRetryAttempts = 3;
    opt.UseExponentialBackoff = true;
    opt.ShouldRetryException = ex => ex is HttpRequestException;
});

// Automatic retries: 200ms -> 400ms -> 800ms
```

### TimeoutBehavior - No More Hanging Requests

Requests without timeouts can hang indefinitely. The behavior enforces timeouts across all requests.

```csharp
cfg.AddTimeoutBehavior(opt =>
{
    opt.DefaultTimeout = TimeSpan.FromSeconds(30);
    opt.SetTimeout<SlowReportQuery>(TimeSpan.FromMinutes(5));
});
```

### Full Pipeline Configuration

```csharp
services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Recommended order: timeout -> retry -> logging -> validation
    cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(30));
    cfg.AddRetryBehavior(opt => opt.MaxRetryAttempts = 3);
    cfg.AddLoggingBehavior();
    cfg.AddValidationBehavior();
});
```

[Full changelog](CHANGELOG.md#310---2025-12-29)

---

## Getting Started

**1. Install the package:**

```bash
dotnet add package MediateX
```

**2. Register services:**

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

**3. Create a request and handler:**

```csharp
public record GetProductQuery(int Id) : IRequest<Product>;

public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    public Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = new Product { Id = request.Id, Name = "Example" };
        return Task.FromResult(product);
    }
}
```

**4. Send the request:**

```csharp
var product = await mediator.Send(new GetProductQuery(42));
```

---

## Features

### Core
- **Request/Response** - Commands and queries with single handler (`IRequest<TResponse>`)
- **Notifications** - Publish to multiple handlers (`INotification`)
- **Streaming** - Async streams with `IAsyncEnumerable<T>` (`IStreamRequest<TResponse>`)
- **Interface Segregation** - Use `ISender`, `IPublisher`, or full `IMediator`
- **Result&lt;T&gt;** - Functional error handling without exceptions

### Pipeline Behaviors
- **ValidationBehavior** - Automatic request validation before handler execution
- **LoggingBehavior** - Zero-allocation logging with slow request detection
- **RetryBehavior** - Automatic retries with exponential backoff and jitter
- **TimeoutBehavior** - Request timeout enforcement with per-request configuration
- **Pre/Post Processors** - Execute logic before and after handlers
- **Exception Handling** - Handle errors and provide fallback responses
- **Stream Behaviors** - Wrap and transform async streams

### Advanced
- **Publishing Strategies** - Sequential or parallel notification execution
- **Generic Handlers** - Register open generic handlers with constraints
- **Dynamic Dispatch** - Runtime type resolution when needed

---

## Versioning Policy

MediateX targets the current .NET LTS version. When a new LTS is released, I drop support for older versions.

| MediateX | .NET | C# | Status |
|----------|------|-----|--------|
| 1.x - 2.x | .NET 9 | C# 13 | Legacy |
| **3.x** | **.NET 10** | **C# 14** | **Current** |

**Future:**

| MediateX | .NET | Notes |
|----------|------|-------|
| 4.x | .NET 10 + 11 | LTS + Standard |
| 5.x | .NET 12 | New LTS (drops 10/11) |

This keeps the codebase clean and lets me use the latest features without compatibility hacks.

---

## Documentation

- [Getting Started](docs/01-getting-started.md) - Installation and basic setup
- [Requests & Handlers](docs/02-requests-handlers.md) - Request types and handler patterns
- [Notifications](docs/03-notifications.md) - Pub/sub and publishing strategies
- [Pipeline Behaviors](docs/04-behaviors.md) - Cross-cutting concerns
- [Configuration](docs/05-configuration.md) - All configuration options
- [Exception Handling](docs/06-exception-handling.md) - Error handling strategies
- [Streaming](docs/07-streaming.md) - Working with `IAsyncEnumerable<T>`

---

## Examples

Working examples for different scenarios and DI containers:

| Example | Description |
|---------|-------------|
| [MediateX.Examples](samples/MediateX.Examples/) | Basic console app with all core features |
| [MediateX.Examples.AspNetCore](samples/MediateX.Examples.AspNetCore/) | ASP.NET Core integration |
| [MediateX.Examples.Autofac](samples/MediateX.Examples.Autofac/) | Autofac DI container |
| [MediateX.Examples.DryIoc](samples/MediateX.Examples.DryIoc/) | DryIoc DI container |
| [MediateX.Examples.Lamar](samples/MediateX.Examples.Lamar/) | Lamar DI container |
| [MediateX.Examples.LightInject](samples/MediateX.Examples.LightInject/) | LightInject DI container |
| [MediateX.Examples.SimpleInjector](samples/MediateX.Examples.SimpleInjector/) | SimpleInjector DI container |
| [MediateX.Examples.Stashbox](samples/MediateX.Examples.Stashbox/) | Stashbox DI container |
| [MediateX.Examples.Windsor](samples/MediateX.Examples.Windsor/) | Castle Windsor DI container |
| [MediateX.Examples.PublishStrategies](samples/MediateX.Examples.PublishStrategies/) | Notification publishing strategies |

---

## License

Apache 2.0. See [LICENSE](LICENSE) for details.

---

<sub>MediateX was originally forked from [MediatR](https://github.com/jbogard/MediatR) 12.5.0 by Jimmy Bogard.</sub>
