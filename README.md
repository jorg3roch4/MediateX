MediateX
=====

**Version 1.0.2** - Based on MediatR 12.5.0

Modern mediator implementation for .NET 9+ with in-process messaging and zero external dependencies.

Supports request/response, commands, queries, notifications and events, synchronous and async with intelligent dispatching via C# generic variance.

## Author & Project Information

**Author:** jorg3roch4
**Project URL:** https://github.com/jorg3roch4/MediateX
**Repository:** https://github.com/jorg3roch4/MediateX
**NuGet Package:** https://www.nuget.org/packages/MediateX

### Project Goals

MediateX is an independent fork maintained specifically for modern .NET versions. The primary goals of this project are:

- ✅ **Target .NET 9+** - Focus exclusively on modern .NET (LTS and STS releases)
- ✅ **Stay Current** - Update to new .NET versions as they become available
- ✅ **Modern C#** - Leverage the latest C# language features and performance improvements
- ✅ **No Legacy Support** - Remove .NET Framework and older .NET Standard dependencies
- ✅ **Performance** - Optimize for the latest .NET runtime improvements
- ✅ **Maintenance** - Provide ongoing updates and bug fixes for the .NET 9+ ecosystem

This project is maintained independently and will continue to evolve with each new .NET release, ensuring developers always have access to a mediator pattern implementation optimized for the latest .NET platform.

## About This Project

**MediateX** is a fork of [MediatR 12.5.0](https://github.com/jbogard/MediatR) by Jimmy Bogard, modernized for .NET 9 and maintained as an independent project. All core functionality and architecture from MediatR 12.5.0 has been preserved, with the project updated to leverage the latest .NET features.

### Requirements
- ✅ **.NET 9.0 or higher** (November 2024 LTS release)
- ✅ **C# 13** (latest language features)
- ⚠️ **No .NET Framework support** - This is a modern .NET only library

### Key Features
- ✅ In-process messaging
- ✅ Request/Response pattern
- ✅ Commands and Queries (CQRS)
- ✅ Notifications and Events (Pub/Sub)
- ✅ Synchronous and asynchronous support
- ✅ Pipeline behaviors for cross-cutting concerns
- ✅ Built-in support for Microsoft.Extensions.DependencyInjection 9.0
- ✅ No external dependencies (except DI abstractions)
- ✅ Optimized for .NET 9 performance

## Installation

Install MediateX via NuGet Package Manager:

```powershell
Install-Package MediateX
```

Or via the .NET CLI:

```bash
dotnet add package MediateX
```

### Contracts-Only Package

For scenarios where you only need the contract interfaces without the full implementation (e.g., shared libraries, API contracts), use the lightweight **MediateX.Contracts** package:

```powershell
Install-Package MediateX.Contracts
```

This package includes:
- `IRequest<TResponse>` and `IRequest` (request interfaces)
- `INotification` (notification interface)
- `IStreamRequest<TResponse>` (streaming request interface)
- `Unit` (void return type)

**Use cases:**
- Shared contract libraries
- API/GRPC contracts
- Blazor projects
- Microservices communication contracts

## Getting Started

### Basic Usage with Dependency Injection

MediateX integrates seamlessly with `Microsoft.Extensions.DependencyInjection`. Register MediateX services and handlers in your startup:

**Register from assembly containing a specific type:**
```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Startup>());
```

**Or register from a specific assembly:**
```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
```

**This automatically registers:**
- `IMediator`, `ISender`, `IPublisher` as transient
- All `IRequestHandler<,>` implementations
- All `INotificationHandler<>` implementations
- All `IStreamRequestHandler<>` implementations
- Exception handlers and actions

### Adding Pipeline Behaviors

Register behaviors for cross-cutting concerns like logging, validation, and transactions:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly);

    // Add pipeline behaviors
    cfg.AddBehavior<LoggingBehavior>();
    cfg.AddBehavior<ValidationBehavior>();

    // Add stream behaviors
    cfg.AddStreamBehavior<LoggingStreamBehavior>();

    // Add pre/post processors
    cfg.AddRequestPreProcessor<LoggingPreProcessor>();
    cfg.AddRequestPostProcessor<LoggingPostProcessor>();

    // Add open generic behaviors
    cfg.AddOpenBehavior(typeof(GenericBehavior<,>));
});
```

## Example Usage

### Request/Response Pattern

```csharp
// Define a request
public class GetUserQuery : IRequest<User>
{
    public int UserId { get; set; }
}

// Define a handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _repository;

    public GetUserQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.UserId, cancellationToken);
    }
}

// Use the mediator
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var user = await _mediator.Send(new GetUserQuery { UserId = id });
        return Ok(user);
    }
}
```

### Notification Pattern (Pub/Sub)

```csharp
// Define a notification
public class UserCreatedNotification : INotification
{
    public int UserId { get; set; }
    public string Email { get; set; }
}

// Define multiple handlers
public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // Send welcome email
    }
}

public class CreateUserProfileHandler : INotificationHandler<UserCreatedNotification>
{
    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // Create user profile
    }
}

// Publish notification
await _mediator.Publish(new UserCreatedNotification { UserId = 1, Email = "user@example.com" });
```

## Credits and Attribution

**MediateX 1.0.2** is based on **[MediatR 12.5.0](https://github.com/jbogard/MediatR)** created by [Jimmy Bogard](https://github.com/jbogard).

All core functionality, architecture, and design patterns are derived from the original MediatR project. MediateX maintains the same Apache 2.0 license and acknowledges the excellent work of Jimmy Bogard and all MediatR contributors.

For the original MediatR documentation and resources, visit: https://github.com/jbogard/MediatR

## License

Copyright (c) 2025 MediateX Project
Copyright (c) Jimmy Bogard (Original MediatR implementation)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
