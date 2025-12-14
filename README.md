![MediateX Logo](https://raw.githubusercontent.com/jorg3roch4/MediateX/main/assets/mediatex-brand.png)

# MediateX

**The Modern Mediator Pattern for .NET 10+**

[![NuGet](https://img.shields.io/nuget/v/MediateX.svg?style=flat-square)](https://www.nuget.org/packages/MediateX)[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg?style=flat-square)](https://github.com/jorg3roch4/MediateX/blob/main/LICENSE)[![C#](https://img.shields.io/badge/C%23-14-239120.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/)[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square)](https://dotnet.microsoft.com/)

**MediateX** is a bold reimagining of the mediator pattern, crafted exclusively for the modern .NET ecosystem. Born from the solid foundation of MediatR, MediateX takes a deliberate step forward, shedding the weight of backward compatibility to fully embrace the power and performance of **.NET 10 and beyond**.

Version 3.x marks a pivotal moment: upgraded to .NET 10, enhanced DI container compatibility, improved assembly scanning robustness, and critical bug fixes for notification handlers and nested generic behaviors.

Our philosophy is simple: always leverage the best of what the .NET platform offers. MediateX is built with the latest **C# 14** features and targets **.NET 10**. This is not just another library; it's a commitment to staying on the cutting edge.

---

## 💖 Support the Project

MediateX is a passion project, driven by the desire to provide a truly modern tool for the .NET community. Maintaining this library requires significant effort: staying current with each .NET release, addressing issues promptly, implementing new features, keeping documentation up to date, and ensuring compatibility across different DI containers.

If MediateX has helped you build better applications or saved you development time, I would be incredibly grateful for your support. Your contribution—no matter the size—helps me dedicate time to respond to issues quickly, implement improvements, and keep the library evolving alongside the .NET platform.

**I'm also looking for sponsors** who believe in this project's mission. Sponsorship helps ensure MediateX remains actively maintained and continues to serve the .NET community for years to come.

Of course, there's absolutely no obligation. If you prefer, simply starring the repository or sharing MediateX with fellow developers is equally appreciated!

- ⭐ **Star the repository** on GitHub to raise its visibility
- 💬 **Share** MediateX with your team or community
- ☕ **Support via Donations:**

  - [![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/jorg3roch4)
  - [![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/jorg3roch4)

---

## 🎉 What's New in 3.0.0

**Major .NET 10 Upgrade!** MediateX 3.0.0 brings significant improvements:

- ⬆️ **Target Framework:** Upgraded from .NET 9 to .NET 10 with C# 14
- 🔧 **DI Container Compatibility:** Simplified mediator constructor works with all DI containers
- 🛡️ **Assembly Scanning Robustness:** Safe handling of assemblies with unloadable types (F# `inref`/`outref`, missing dependencies)
- 🐛 **Notification Handler Fix:** Fixed contravariance-based incorrect handler registration that caused duplicate invocations
- 🐛 **Nested Generic Behaviors:** Fixed `AddOpenBehavior()` for behaviors like `IPipelineBehavior<TRequest, Result<T>>`
- 📁 **Project Structure:** Reorganized to `src/MediateX/` following .NET ecosystem conventions

[See the full changelog](CHANGELOG.md#300---2025-12-13) for details.

---

## 🚀 Getting Started

Integrating MediateX into your .NET 10+ application is straightforward.

**1. Install the NuGet Package:**
```bash
dotnet add package MediateX
```

**2. Register Services:**
In your `Program.cs` or service configuration, register MediateX and tell it which assembly to scan for handlers.

```csharp
builder.Services.AddMediateX(cfg =>
{
    // Scans the assembly containing your handlers (e.g., Program)
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

**3. Create and Send a Request:**
Define a request and its handler, then use the `IMediator` service to send it.

```csharp
// Define a request and its expected response
public record GetProductQuery(int Id) : IRequest<Product>;

// Create a handler for the request
public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    public Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Your business logic here...
        var product = new Product { Id = request.Id, Name = "Awesome Gadget" };
        return Task.FromResult(product);
    }
}

// Inject IMediator and send the request from a controller or another service
var product = await mediator.Send(new GetProductQuery(42));
```

---

## ✨ Features

### Core Capabilities
- **Request/Response** - Commands and queries with single handler (`IRequest<TResponse>`)
- **Notifications & Events** - Publish to multiple handlers (`INotification`)
- **Streaming** - Async stream responses with `IAsyncEnumerable<T>` (`IStreamRequest<TResponse>`)
- **Separated Interfaces** - Use `ISender`, `IPublisher`, or `IMediator` based on your needs

### Pipeline Features
- **Behaviors** - Add cross-cutting concerns (logging, validation, caching, etc.)
- **Pre/Post Processors** - Execute logic before and after handlers
- **Exception Handling** - Sophisticated error handling with fallback responses
- **Stream Behaviors** - Wrap and transform async streams

### Advanced Features
- **Publishing Strategies** - Sequential or parallel notification execution
- **Generic Variance** - Leverage C# covariance/contravariance
- **Dynamic Dispatch** - Runtime type resolution for flexibility
- **Handler Prioritization** - Automatic ordering based on assembly/namespace
- **Open Generic Support** - Register generic handlers with constraints

---

## 📅 Versioning & .NET Support Policy

MediateX follows a clear versioning strategy aligned with .NET's release cadence:

### Version History

| MediateX | .NET | C# | Status |
|----------|------|-----|--------|
| 1.x - 2.x | .NET 9 | C# 13 | Legacy |
| **3.x** | **.NET 10** | **C# 14** | **Current** |

### Future Support Policy

MediateX will always support the **current LTS version** plus the **next standard release**. When a new LTS version is released, support for older versions will be discontinued:

| MediateX | .NET | C# | Notes |
|----------|------|-----|-------|
| 3.x | .NET 10 | C# 14 | LTS only |
| 4.x | .NET 10 + .NET 11 | C# 14 / C# 15 | LTS + Standard |
| 5.x | .NET 12 | C# 16 | New LTS (drops .NET 10/11) |
| 6.x | .NET 12 + .NET 13 | C# 16 / C# 17 | LTS + Standard |

**Why this policy?**
- **Focused development:** By limiting supported versions, we can dedicate more effort to quality, performance, and new features
- **Modern features:** Each .NET version brings improvements that MediateX can fully leverage
- **Clear upgrade path:** Users know exactly when to plan their upgrades

> **Note:** We recommend always using the latest LTS version of .NET for production applications.

---

## 📚 Documentation

Comprehensive guides to help you master MediateX:

### Getting Started
- **[Getting Started](https://github.com/jorg3roch4/MediateX/blob/main/docs/01-getting-started.md)** - Installation, basic setup, and first request
- **[Requests & Handlers](https://github.com/jorg3roch4/MediateX/blob/main/docs/02-requests-handlers.md)** - Deep dive into requests, handlers, and best practices

### Core Features
- **[Notifications & Events](https://github.com/jorg3roch4/MediateX/blob/main/docs/03-notifications.md)** - Pub/sub pattern, publishing strategies, event handling
- **[Pipeline Behaviors](https://github.com/jorg3roch4/MediateX/blob/main/docs/04-behaviors.md)** - Cross-cutting concerns, behavior chaining, examples
- **[Configuration](https://github.com/jorg3roch4/MediateX/blob/main/docs/05-configuration.md)** - Complete configuration reference and options

### Advanced Topics
- **[Exception Handling](https://github.com/jorg3roch4/MediateX/blob/main/docs/06-exception-handling.md)** - Exception handlers, actions, and strategies
- **[Streaming](https://github.com/jorg3roch4/MediateX/blob/main/docs/07-streaming.md)** - Working with `IAsyncEnumerable<T>` and stream behaviors

### Examples
Check out the **[samples folder](https://github.com/jorg3roch4/MediateX/tree/main/samples)** for complete working examples with different DI containers and scenarios.

---

## 🙏 Acknowledgments

MediateX would not exist without the pioneering work of **Jimmy Bogard** and the many contributors to the original **MediatR** project. Their efforts created a robust and battle-tested foundation that has benefited countless .NET developers.

This library began as a fork of **MediatR version 12.5.0**. We are immensely grateful for their contribution to the .NET ecosystem, which provided the starting point for this new, forward-focused direction. While MediatR continues to serve a broad range of .NET versions, MediateX is our dedicated effort to serve developers who are ready to build exclusively on the future of the platform.

**Original Project:** [MediatR on GitHub](https://github.com/jbogard/MediatR)