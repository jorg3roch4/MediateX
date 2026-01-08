![MediateX Logo](https://raw.githubusercontent.com/jorg3roch4/MediateX/main/assets/mediatex-brand.png)

# MediateX

**More than a mediator. A complete request processing framework for .NET 10+**

[![NuGet](https://img.shields.io/nuget/v/MediateX.svg?style=flat-square)](https://www.nuget.org/packages/MediateX) [![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg?style=flat-square)](https://github.com/jorg3roch4/MediateX/blob/main/LICENSE) [![C#](https://img.shields.io/badge/C%23-14-239120.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/) [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square)](https://dotnet.microsoft.com/)

---

## Support the Project

MediateX is free and always will be. If it helps your projects, consider supporting development:

[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/jorg3roch4) [![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/jorg3roch4)

A GitHub star helps too.

---

## At a Glance

| Category | What You Get |
|----------|--------------|
| **Core** | Request/Response, Notifications, Streaming (`IAsyncEnumerable`), Dynamic Dispatch |
| **Result&lt;T&gt;** | Functional error handling with `Map`, `Bind`, `Match` - in `MediateX.Contracts` namespace |
| **Validation** | Built-in validators with fluent API - no FluentValidation dependency |
| **Behaviors** | Logging, Retry (exponential backoff + jitter), Timeout, Validation - ready to use |
| **Exception Handling** | Hierarchical handlers by exception type with recovery options |
| **Publishing** | Sequential or parallel notification strategies, swappable at runtime |
| **DI Containers** | 9 containers supported (MS DI, Autofac, DryIoc, Lamar, and more) |
| **Advanced** | Nested generic behaviors, handler deduplication, F# compatibility, generic handler auto-closing |

---

## Quick Start

```bash
dotnet add package MediateX
```

```csharp
// 1. Configure
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddLoggingBehavior();
    cfg.AddValidationBehavior();
});

// 2. Define request (Result<T> is in MediateX.Contracts namespace)
using MediateX.Contracts;

public record GetUserQuery(int Id) : IRequest<Result<User>>;

// 3. Handle it
public class GetUserHandler : IRequestHandler<GetUserQuery, Result<User>>
{
    public async Task<Result<User>> Handle(GetUserQuery query, CancellationToken ct)
    {
        var user = await _repo.Find(query.Id);
        return user is null
            ? Result<User>.Failure("NotFound", "User not found")
            : Result<User>.Success(user);
    }
}

// 4. Use it
var result = await mediator.Send(new GetUserQuery(42));
return result.Match(
    onSuccess: user => Ok(user),
    onFailure: error => NotFound(error.Message)
);
```

---

## Features

### Result&lt;T&gt; - Railroad Oriented Programming

```csharp
using MediateX.Contracts; // Result<T>, Error, ResultExtensions

// Chain operations without try/catch
var result = await GetUser(id)
    .Bind(user => ValidateUser(user))
    .Map(user => new UserDto(user));

// Pattern match the outcome
return result.Match(
    onSuccess: dto => Ok(dto),
    onFailure: error => BadRequest(error)
);
```

### Built-in Behaviors

```csharp
cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(30));
cfg.AddRetryBehavior(opt => opt.MaxRetryAttempts = 3);
cfg.AddLoggingBehavior(opt => opt.SlowRequestThresholdMs = 500);
cfg.AddValidationBehavior();
```

### Validation Without Dependencies

```csharp
public class CreateUserValidator : IRequestValidator<CreateUserCommand>
{
    public ValueTask<ValidationResult> ValidateAsync(CreateUserCommand cmd, CancellationToken ct)
        => ValueTask.FromResult(
            new ValidationResultBuilder()
                .RequireNotEmpty(cmd.Name, "Name")
                .RequireNotEmpty(cmd.Email, "Email")
                .RequireMaxLength(cmd.Email, 100, "Email")
                .Build());
}
```

### Exception Handling with Recovery

```csharp
public class DatabaseExceptionHandler : IRequestExceptionHandler<MyRequest, MyResponse, DbException>
{
    public Task Handle(MyRequest req, DbException ex, RequestExceptionHandlerState<MyResponse> state, CancellationToken ct)
    {
        if (ex.IsTransient)
            state.SetHandled(MyResponse.RetryLater());
        return Task.CompletedTask;
    }
}
```

### Streaming

```csharp
public record GetAllProducts : IStreamRequest<Product>;

public class Handler : IStreamRequestHandler<GetAllProducts, Product>
{
    public async IAsyncEnumerable<Product> Handle(GetAllProducts request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var product in _repo.StreamAll(ct))
            yield return product;
    }
}
```

---

## Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/01-getting-started.md) | Installation and basic setup |
| [Requests & Handlers](docs/02-requests-handlers.md) | Request types and handler patterns |
| [Notifications](docs/03-notifications.md) | Pub/sub and publishing strategies |
| [Pipeline Behaviors](docs/04-behaviors.md) | Cross-cutting concerns |
| [Configuration](docs/05-configuration.md) | All configuration options |
| [Exception Handling](docs/06-exception-handling.md) | Error handling strategies |
| [Streaming](docs/07-streaming.md) | Working with `IAsyncEnumerable<T>` |

---

## Examples

| Example | Description |
|---------|-------------|
| [MediateX.Examples](samples/MediateX.Examples/) | Basic console app with all core features |
| [MediateX.Examples.AspNetCore](samples/MediateX.Examples.AspNetCore/) | ASP.NET Core integration with v3.1.0 behaviors |
| [MediateX.Examples.Autofac](samples/MediateX.Examples.Autofac/) | Autofac DI container |
| [MediateX.Examples.DryIoc](samples/MediateX.Examples.DryIoc/) | DryIoc DI container |
| [MediateX.Examples.Lamar](samples/MediateX.Examples.Lamar/) | Lamar DI container |
| [MediateX.Examples.LightInject](samples/MediateX.Examples.LightInject/) | LightInject DI container |
| [MediateX.Examples.SimpleInjector](samples/MediateX.Examples.SimpleInjector/) | SimpleInjector DI container |
| [MediateX.Examples.Stashbox](samples/MediateX.Examples.Stashbox/) | Stashbox DI container |
| [MediateX.Examples.Windsor](samples/MediateX.Examples.Windsor/) | Castle Windsor DI container |
| [MediateX.Examples.PublishStrategies](samples/MediateX.Examples.PublishStrategies/) | Notification publishing strategies |

---

## Versioning

MediateX targets current .NET LTS. When a new LTS is released, older versions are dropped.

| MediateX | .NET | Status |
|----------|------|--------|
| 1.x - 2.x | .NET 9 | Legacy |
| **3.x** | **.NET 10** | **Current** |

---

## License

Apache 2.0. See [LICENSE](LICENSE) for details.

---

<sub>MediateX was originally forked from [MediatR](https://github.com/jbogard/MediatR) 12.5.0 by Jimmy Bogard.</sub>
