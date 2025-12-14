# MediateX Documentation

Welcome to the MediateX documentation! This guide will help you master the modern mediator pattern for .NET 10+.

---

## 📖 Table of Contents

### Getting Started
1. **[Getting Started](./01-getting-started.md)**
   - Installation
   - Basic setup and configuration
   - Your first request and handler
   - Notifications (Pub/Sub)
   - CQRS pattern
   - Best practices and troubleshooting

2. **[Requests & Handlers](./02-requests-handlers.md)**
   - Request types (`IRequest<TResponse>`, `IRequest`)
   - Handler types and implementation
   - Request guidelines and patterns
   - Handler best practices
   - Advanced patterns (generic handlers, polymorphism, validation)

### Core Features

3. **[Notifications & Events](./03-notifications.md)**
   - Notification interface and handlers
   - Multiple handlers per notification
   - Publishing strategies (sequential vs parallel)
   - Custom publishers
   - Domain events and event sourcing
   - Best practices and testing

4. **[Pipeline Behaviors](./04-behaviors.md)**
   - Pipeline behavior interface
   - Common use cases (logging, validation, caching, transactions)
   - Behavior execution order
   - Open vs closed generic behaviors
   - Registration and configuration
   - Multiple behaviors chaining
   - Advanced patterns and testing

5. **[Configuration](./05-configuration.md)**
   - Complete configuration reference
   - Assembly registration options
   - Service lifetime management
   - Type filtering
   - Behavior registration
   - Publishing strategies
   - Exception handling configuration
   - Generic handler support
   - Environment-specific configuration
   - Troubleshooting

### Advanced Topics

6. **[Exception Handling](./06-exception-handling.md)**
   - Exception handler interface
   - Exception handler state management
   - Exception actions
   - Exception hierarchy support
   - Multiple exception handlers
   - Processing strategies
   - Built-in behaviors
   - Best practices and patterns

7. **[Streaming](./07-streaming.md)**
   - Stream request interface
   - Stream handler implementation
   - `IAsyncEnumerable<T>` patterns
   - Stream pipeline behaviors
   - Cancellation support
   - Use cases (large datasets, real-time data, batch processing)
   - ASP.NET Core integration
   - Best practices and performance

---

## 🚀 Quick Navigation

### By Task

**I want to...**
- **Get started quickly** → [Getting Started](./01-getting-started.md)
- **Understand requests** → [Requests & Handlers](./02-requests-handlers.md)
- **Send events to multiple handlers** → [Notifications & Events](./03-notifications.md)
- **Add logging/validation** → [Pipeline Behaviors](./04-behaviors.md)
- **Configure MediateX** → [Configuration](./05-configuration.md)
- **Handle errors gracefully** → [Exception Handling](./06-exception-handling.md)
- **Work with async streams** → [Streaming](./07-streaming.md)

### By Concept

**CQRS** → [Getting Started](./01-getting-started.md#cqrs-pattern), [Requests & Handlers](./02-requests-handlers.md)

**Domain Events** → [Notifications & Events](./03-notifications.md)

**Cross-Cutting Concerns** → [Pipeline Behaviors](./04-behaviors.md)

**Validation** → [Pipeline Behaviors](./04-behaviors.md), [Exception Handling](./06-exception-handling.md)

**Error Handling** → [Exception Handling](./06-exception-handling.md)

**Async Streams** → [Streaming](./07-streaming.md)

**Dependency Injection** → [Getting Started](./01-getting-started.md#dependency-injection), [Configuration](./05-configuration.md)

---

## 📋 Documentation Standards

All documentation follows these standards:

- **Code Examples**: All examples use modern C# 9-14 features with .NET 10+
  - Collection expressions (`[]` syntax)
  - Target-typed new expressions
  - Pattern matching
  - Records for requests
- **Async/Await**: Proper async patterns with `CancellationToken` support
- **Best Practices**: Each guide includes best practices section
- **Examples**: Real-world, practical examples
- **Cross-References**: Links to related documentation

---

## 🛠️ Examples

For complete working examples, see the **[samples folder](../samples/)** which includes:

- ASP.NET Core integration
- Different DI containers (Autofac, SimpleInjector, Windsor, etc.)
- Publishing strategies
- Custom implementations

---

## 💡 Tips for Learning

1. **Start with Getting Started** - Follow the guide sequentially if you're new
2. **Try the examples** - Run the sample projects to see MediateX in action
3. **Read best practices** - Each guide has a best practices section
4. **Check troubleshooting** - Common issues and solutions are documented
5. **Explore advanced topics** - Once comfortable with basics, dive into behaviors and streaming

---

## 🔗 External Resources

- **[GitHub Repository](https://github.com/jorg3roch4/MediateX)**
- **[NuGet Package](https://www.nuget.org/packages/MediateX)**
- **[Samples](../samples/)**
- **[Issue Tracker](https://github.com/jorg3roch4/MediateX/issues)**

---

## 📝 Contributing to Documentation

Found an error or want to improve the docs? Contributions are welcome!

1. Fork the repository
2. Make your changes
3. Submit a pull request

---

**Ready to get started?** Begin with **[Getting Started](./01-getting-started.md)** and build your first MediateX application!
