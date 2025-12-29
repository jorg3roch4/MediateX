# MediateX.Examples.Lamar

MediateX with the [Lamar](https://jasperfx.github.io/lamar/) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.Lamar
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Lamar container setup with fluent scanning API |

## Setup

```csharp
var container = new Container(cfg =>
{
    cfg.Scan(scanner =>
    {
        scanner.AssemblyContainingType<Ping>();
        scanner.AssemblyContainingType<Mediator>();
        scanner.ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>));
        scanner.ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>));
    });

    // Explicit behavior registration for ordering
    cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(GenericPipelineBehavior<,>));

    cfg.For<IMediator>().Use<Mediator>().Transient();
});
```

## Notes

- Lamar uses a fluent scanning API with `ConnectImplementationsToTypesClosing()`
- Explicit `.Add()` calls let you control behavior order
- `Transient` lifetime for Mediator (different from default)
- Developed by the Jasper team

## Dependencies

```xml
<PackageReference Include="Lamar" Version="15.0.1" />
```
