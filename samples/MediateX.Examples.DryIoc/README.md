# MediateX.Examples.DryIoc

MediateX with the [DryIoc](https://github.com/dadhi/DryIoc) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.DryIoc
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | DryIoc container setup with `RegisterMany()` |

## Setup

```csharp
var container = new Container();

// Register instance
container.Use<TextWriter>(writer);

// Auto-register from assembly
container.RegisterMany(new[] { typeof(Ping).Assembly, typeof(Mediator).Assembly },
    type => type.GetInterfaces().Any(i =>
        i.IsGenericType &&
        i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

// Register Mediator with specific constructor
container.Register<IMediator, Mediator>(made: Made.Of(
    () => new Mediator(Arg.Of<IServiceProvider>())));

var services = new ServiceCollection();
var provider = container.WithDependencyInjectionAdapter(services).BuildServiceProvider();
```

## Notes

- DryIoc is fast and lightweight
- `RegisterMany()` handles bulk registration with filtering
- Automatic pipeline behavior discovery
- Simpler setup than Autofac

## Dependencies

```xml
<PackageReference Include="DryIoc.Microsoft.DependencyInjection" />
```
