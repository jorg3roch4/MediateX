# MediateX.Examples.AspNetCore

MediateX with the built-in .NET DI container (`Microsoft.Extensions.DependencyInjection`).

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.AspNetCore
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Standard .NET DI setup with `AddMediateX()` |

## Setup

```csharp
var services = new ServiceCollection();

services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblies(typeof(Ping).Assembly);
});

// Manual registration for behaviors and processors
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(GenericPipelineBehavior<,>));
services.AddScoped(typeof(IRequestPreProcessor<>), typeof(GenericRequestPreProcessor<>));
services.AddScoped(typeof(IRequestPostProcessor<,>), typeof(GenericRequestPostProcessor<,>));

var provider = services.BuildServiceProvider();
```

## Notes

This is the simplest setup. Use this approach for:
- ASP.NET Core applications
- Console apps using `Microsoft.Extensions.DependencyInjection`
- When you don't need a third-party DI container
