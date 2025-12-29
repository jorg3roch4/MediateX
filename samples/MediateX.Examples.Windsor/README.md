# MediateX.Examples.Windsor

MediateX with the [Castle Windsor](http://www.castleproject.org/projects/windsor/) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.Windsor
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Castle Windsor container setup with custom IServiceProvider adapter |

## Setup

```csharp
var container = new WindsorContainer();
container.Kernel.Resolver.AddSubResolver(new CollectionResolver(container.Kernel));
container.Kernel.AddHandlersFilter(new ContravariantFilter());

// Register handlers from assembly
var fromAssemblyContainingPing = Classes.FromAssemblyContaining<Ping>();
container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestHandler<,>)).WithServiceAllInterfaces().AllowMultipleMatches());
container.Register(fromAssemblyContainingPing.BasedOn(typeof(INotificationHandler<>)).WithServiceAllInterfaces().AllowMultipleMatches());

// Pipeline behaviors
container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestPreProcessorBehavior<,>)).NamedAutomatically("PreProcessorBehavior"));
container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestPostProcessorBehavior<,>)).NamedAutomatically("PostProcessorBehavior"));
container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(GenericPipelineBehavior<,>)).NamedAutomatically("Pipeline"));

// Custom IServiceProvider adapter
var serviceProvider = new WindsorServiceProvider(container);
container.Register(Component.For<IServiceProvider>().Instance(serviceProvider));
container.Register(Component.For<IMediator>().ImplementedBy<Mediator>());
```

## Notes

- Castle Windsor is one of the oldest .NET DI containers, mature and feature-rich
- Default lifestyle is Singleton (consider Per Web Request for ASP.NET)
- `CollectionResolver` enables resolving `IEnumerable<T>` dependencies
- `ContravariantFilter` supports contravariant handler resolution
- `NamedAutomatically()` required when registering multiple implementations of the same generic interface
- Requires a custom `IServiceProvider` adapter (included in Program.cs)
- Demonstrates full feature set including exception handling and streaming

## Dependencies

```xml
<PackageReference Include="Castle.Windsor" Version="6.0.0" />
```
