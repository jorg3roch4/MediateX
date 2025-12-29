# MediateX.Examples.Autofac

MediateX with the [Autofac](https://autofac.org/) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.Autofac
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Autofac container setup with explicit handler and behavior registration |

## Setup

```csharp
var builder = new ContainerBuilder();

// Register MediateX
builder.RegisterType<Mediator>().As<IMediator>().InstancePerLifetimeScope();

// Auto-register handlers from assembly
builder
    .RegisterAssemblyTypes(typeof(Ping).Assembly)
    .AsClosedTypesOf(typeof(IRequestHandler<,>))
    .AsImplementedInterfaces();

// Register pipeline behaviors (order matters)
builder.RegisterGeneric(typeof(GenericPipelineBehavior<,>)).As(typeof(IPipelineBehavior<,>));

var container = builder.Build();
var provider = new AutofacServiceProvider(container);
```

## Notes

- Autofac requires explicit registration of open generic types
- Behavior registration order determines execution order
- Uses `AutofacServiceProvider` adapter from `Autofac.Extensions.DependencyInjection`
- More verbose than built-in DI, but offers advanced features like modules and decorators

## Dependencies

```xml
<PackageReference Include="Autofac" Version="9.0.0" />
<PackageReference Include="Autofac.Extensions.DependencyInjection" Version="10.0.0" />
```
