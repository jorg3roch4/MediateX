# MediateX.Examples.LightInject

MediateX with the [LightInject](https://www.lightinject.net/) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.LightInject
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | LightInject container setup with custom IServiceProvider adapter |

## Setup

```csharp
ServiceContainer container = new(ContainerOptions.Default);

// Register handlers from assembly
container.RegisterAssembly(typeof(Ping).GetTypeInfo().Assembly, (serviceType, implementingType) =>
    serviceType.IsConstructedGenericType &&
    (
        serviceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
        serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>)
    ));

// Pipeline behaviors with ordering
container.RegisterOrdered(typeof(IPipelineBehavior<,>),
[
    typeof(RequestPreProcessorBehavior<,>),
    typeof(RequestPostProcessorBehavior<,>),
    typeof(GenericPipelineBehavior<,>)
], type => new PerContainerLifetime());

// Custom IServiceProvider adapter
var serviceProvider = new LightInjectServiceProvider(container);
container.RegisterInstance<IServiceProvider>(serviceProvider);
container.Register<IMediator, Mediator>(new PerContainerLifetime());
```

## Notes

- LightInject requires a custom `IServiceProvider` adapter (included in Program.cs)
- `RegisterOrdered()` controls behavior execution order
- `RegisterAssembly()` with filter function for selective registration
- Uses `PerContainerLifetime` for singleton behavior

## Dependencies

```xml
<PackageReference Include="LightInject" Version="7.0.2" />
```
