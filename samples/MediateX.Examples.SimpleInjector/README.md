# MediateX.Examples.SimpleInjector

MediateX with the [Simple Injector](https://simpleinjector.org/) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.SimpleInjector
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Simple Injector container setup with MS.DI integration |

## Setup

```csharp
Container container = new();
ServiceCollection services = new();

services.AddSimpleInjector(container);

// Register handlers
container.RegisterSingleton<IMediator, Mediator>();
container.Register(typeof(IRequestHandler<,>), assemblies);

// Pipeline behaviors (order matters)
container.Collection.Register(typeof(IPipelineBehavior<,>), new[]
{
    typeof(RequestExceptionProcessorBehavior<,>),
    typeof(RequestExceptionActionProcessorBehavior<,>),
    typeof(RequestPreProcessorBehavior<,>),
    typeof(RequestPostProcessorBehavior<,>),
    typeof(GenericPipelineBehavior<,>)
});

// Stream behaviors
container.Collection.Register(typeof(IStreamPipelineBehavior<,>), new[]
{
    typeof(GenericStreamPipelineBehavior<,>)
});

var serviceProvider = services.BuildServiceProvider().UseSimpleInjector(container);
```

## Notes

- Simple Injector is known for its strict verification and clear error messages
- Uses `Collection.Register()` for multiple implementations of the same interface
- `IncludeGenericTypeDefinitions = true` required for constrained generic handlers
- Integrates with MS.DI via `AddSimpleInjector()` and `UseSimpleInjector()`
- Demonstrates full feature set including exception handling and streaming

## Dependencies

```xml
<PackageReference Include="SimpleInjector" Version="5.5.0" />
<PackageReference Include="SimpleInjector.Integration.ServiceCollection" Version="5.5.0" />
```
