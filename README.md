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

## What's New in 3.0.0

Version 3.0.0 upgrades to .NET 10 and includes several fixes:

- **Target Framework:** .NET 10 with C# 14
- **DI Container Fix:** Simplified constructor works with all containers (Lamar, SimpleInjector, etc.)
- **Assembly Scanning:** Safe handling of assemblies with unloadable types (F# `inref`/`outref`)
- **Notification Handler Fix:** Fixed contravariance bug that caused duplicate handler invocations
- **Nested Generic Behaviors:** `AddOpenBehavior()` now works for `IPipelineBehavior<TRequest, Result<T>>`

[Full changelog](CHANGELOG.md#300---2025-12-13)

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

### Pipeline
- **Behaviors** - Cross-cutting concerns (logging, validation, caching)
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
