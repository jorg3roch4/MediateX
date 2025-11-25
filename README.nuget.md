# MediateX

**The Modern Mediator Pattern for .NET 9+**

[![NuGet](https://img.shields.io/nuget/v/MediateX.svg?style=flat-square)](https://www.nuget.org/packages/MediateX)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg?style=flat-square)](https://github.com/jorg3roch4/MediateX/blob/main/LICENSE)

**MediateX** is a bold reimagining of the mediator pattern, crafted exclusively for the modern .NET ecosystem. Born from the solid foundation of MediatR, MediateX takes a deliberate step forward, shedding the weight of backward compatibility to fully embrace the power and performance of **.NET 9 and beyond**.

Version 2.0.0 marks a pivotal moment: a complete architectural overhaul to unify the project into a single, streamlined package. This enhances performance, simplifies dependency management, and paves the way for rapid, forward-focused development.

Our philosophy is simple: always leverage the best of what the .NET platform offers. MediateX is built with the latest **C# 13** features, and our roadmap is already targeting **.NET 10 and C# 14 for Version 3.0**. This is not just another library; it's a commitment to staying on the cutting edge.

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

- **Request/Response** - Commands and queries with single handler
- **Notifications & Events** - Publish to multiple handlers
- **Streaming** - Async stream responses with `IAsyncEnumerable<T>`
- **Pipeline Behaviors** - Add logging, validation, caching, etc.
- **Exception Handling** - Sophisticated error handling with fallback responses
- **Publishing Strategies** - Sequential or parallel notification execution

---

## 📚 Documentation

Comprehensive guides available on [GitHub](https://github.com/jorg3roch4/MediateX/tree/main/docs):

- **[Getting Started](https://github.com/jorg3roch4/MediateX/blob/main/docs/01-getting-started.md)** - Installation and basic setup
- **[Requests & Handlers](https://github.com/jorg3roch4/MediateX/blob/main/docs/02-requests-handlers.md)** - Deep dive into requests and handlers
- **[Notifications & Events](https://github.com/jorg3roch4/MediateX/blob/main/docs/03-notifications.md)** - Pub/sub pattern and events
- **[Pipeline Behaviors](https://github.com/jorg3roch4/MediateX/blob/main/docs/04-behaviors.md)** - Cross-cutting concerns
- **[Configuration](https://github.com/jorg3roch4/MediateX/blob/main/docs/05-configuration.md)** - Complete configuration reference
- **[Exception Handling](https://github.com/jorg3roch4/MediateX/blob/main/docs/06-exception-handling.md)** - Advanced error handling
- **[Streaming](https://github.com/jorg3roch4/MediateX/blob/main/docs/07-streaming.md)** - Working with async streams

---

## 💖 Support the Project

MediateX is a passion project, driven by the desire to provide a truly modern tool for the .NET community. If you find this library valuable and believe in its forward-thinking philosophy, please consider supporting its development.

- ⭐ **[Star the repository on GitHub](https://github.com/jorg3roch4/MediateX)** to raise its visibility
- ☕ **Support via Donations:**
  - [PayPal](https://paypal.me/jorg3roch4)
  - [Ko-fi](https://ko-fi.com/jorg3roch4)

---

## 🙏 Acknowledgments

MediateX would not exist without the pioneering work of **Jimmy Bogard** and the many contributors to the original **MediatR** project. Their efforts created a robust and battle-tested foundation that has benefited countless .NET developers.

This library began as a fork of **MediatR version 12.5.0**. We are immensely grateful for their contribution to the .NET ecosystem, which provided the starting point for this new, forward-focused direction. While MediatR continues to serve a broad range of .NET versions, MediateX is our dedicated effort to serve developers who are ready to build exclusively on the future of the platform.

**Original Project:** [MediatR on GitHub](https://github.com/jbogard/MediatR)
