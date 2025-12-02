![MediateX Logo](https://raw.githubusercontent.com/jorg3roch4/MediateX/main/assets/MediateX.png)

# MediateX

**The Modern Mediator Pattern for .NET 9+**

[![NuGet](https://img.shields.io/nuget/v/MediateX.svg?style=flat-square)](https://www.nuget.org/packages/MediateX)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg?style=flat-square)](https://github.com/jorg3roch4/MediateX/blob/main/LICENSE)
[![C#](https://img.shields.io/badge/C%23-13-239120.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg?style=flat-square)](https://dotnet.microsoft.com/)

**MediateX** is a bold reimagining of the mediator pattern, crafted exclusively for the modern .NET ecosystem. Born from the solid foundation of MediatR, MediateX takes a deliberate step forward, shedding the weight of backward compatibility to fully embrace the power and performance of **.NET 9 and beyond**.

Version 2.x marks a pivotal moment: a complete architectural overhaul to unify the project into a single, streamlined package. This enhances performance, simplifies dependency management, and paves the way for rapid, forward-focused development.

Our philosophy is simple: always leverage the best of what the .NET platform offers. MediateX is built with the latest **C# 13** features, and our roadmap is already targeting **.NET 10 and C# 14 for Version 3.0**. This is not just another library; it's a commitment to staying on the cutting edge.

---

## 💖 Support the Project

MediateX is a passion project, driven by the desire to provide a truly modern tool for the .NET community. If you find this library valuable and believe in its forward-thinking philosophy, please consider supporting its development. Every contribution helps sustain the effort required to keep the project aligned with the rapid pace of .NET innovation.

- ⭐ **Star the repository** on GitHub to raise its visibility.
- ☕ **Support via Donations:**

  - [![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/jorg3roch4)
  - [![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/jorg3roch4)

---

## 🎉 What's New in 2.1.0

**Full C# 9-13 Modernization!** MediateX 2.1.0 represents a complete code modernization effort:

- ✨ **Collection Expressions (C# 12):** Clean, concise collection initialization with `[]` syntax
- ✨ **Pattern Matching:** Advanced tuple patterns, relational patterns, and or patterns throughout
- ✨ **Target-Typed New (C# 9):** Reduced verbosity in 50+ instantiations
- ✨ **ArgumentNullException.ThrowIfNull (C# 11):** Modern parameter validation
- 🚀 **Performance:** Optimized collection operations and pattern matching
- 📉 **Reduced Code:** ~80 lines less code while improving readability

[See the full changelog](CHANGELOG.md#210---2025-12-01) for details.

---

## 🚀 Getting Started

Integrating MediateX into your .NET 9+ application is straightforward.

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