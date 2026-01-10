![MediateX Logo](https://raw.githubusercontent.com/jorg3roch4/MediateX/main/assets/mediatex-brand.png)

# MediateX

**A pure mediator for .NET 10+**

In-process messaging done right. Request/response, notifications, and streaming with a clean pipeline architecture.

[![NuGet](https://img.shields.io/nuget/v/MediateX.svg?style=flat-square)](https://www.nuget.org/packages/MediateX) [![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg?style=flat-square)](LICENSE)

---

## Support

If MediateX helps your projects, consider supporting its development:

[![GitHub stars](https://img.shields.io/github/stars/jorg3roch4/MediateX?style=social)](https://github.com/jorg3roch4/MediateX)
[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=flat-square&logo=paypal&logoColor=white)](https://paypal.me/jorg3roch4)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/jorg3roch4)

---

## Install

```bash
dotnet add package MediateX
```

## Usage

```csharp
// Setup
builder.Services.AddMediateX(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>());

// Request
public record GetUser(int Id) : IRequest<User>;

// Handler
public class GetUserHandler : IRequestHandler<GetUser, User>
{
    public Task<User> Handle(GetUser request, CancellationToken ct)
        => _db.Users.FindAsync(request.Id, ct);
}

// Send
var user = await mediator.Send(new GetUser(42));
```

## Features

- **Request/Response** - `IRequest<T>`, `IRequestHandler<,>`
- **Notifications** - `INotification`, `INotificationHandler<>`
- **Streaming** - `IStreamRequest<T>` with `IAsyncEnumerable`
- **Pipeline Behaviors** - `IPipelineBehavior<,>` for cross-cutting concerns
- **Exception Handling** - `IRequestExceptionHandler<,,>` with recovery

## Docs

Full documentation at [docs/](docs/)

---

~2,700 lines of code · 1 dependency · 168 tests

Apache 2.0 | Based on [MediatR](https://github.com/jbogard/MediatR) 12.5.0
