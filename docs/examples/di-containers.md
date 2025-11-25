# Dependency Injection Container Examples

MediateX works with various popular DI containers. Below are examples for different IoC containers.

## Available Examples

### 🔷 Microsoft.Extensions.DependencyInjection (Built-in)

**📁 Location:** [`samples/MediateX.Examples.AspNetCore/`](../../samples/MediateX.Examples.AspNetCore/)

The default and recommended DI container for .NET applications.

```csharp
services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>()
);
```

[View full ASP.NET Core example →](aspnetcore.md)

---

### 🔷 Autofac

**📁 Location:** [`samples/MediateX.Examples.Autofac/`](../../samples/MediateX.Examples.Autofac/)

Popular IoC container with advanced features like module scanning and decorators.

```csharp
var builder = new ContainerBuilder();

// Register MediateX
builder.RegisterSource(new ContravariantRegistrationSource());
builder
    .RegisterType<Mediator>()
    .As<IMediator>()
    .InstancePerLifetimeScope();

// Register handlers
builder
    .RegisterAssemblyTypes(typeof(Ping).Assembly)
    .AsClosedTypesOf(typeof(IRequestHandler<,>))
    .AsImplementedInterfaces();

builder
    .RegisterAssemblyTypes(typeof(Ping).Assembly)
    .AsClosedTypesOf(typeof(INotificationHandler<>))
    .AsImplementedInterfaces();
```

**Key Features:**
- Contravariant handler registration
- Module-based configuration
- Lifetime scope support

---

### 🔷 Lamar

**📁 Location:** [`samples/MediateX.Examples.Lamar/`](../../samples/MediateX.Examples.Lamar/)

Successor to StructureMap with excellent performance characteristics.

```csharp
var container = new Container(cfg =>
{
    cfg.Scan(scanner =>
    {
        scanner.AssemblyContainingType<Ping>();
        scanner.AssemblyContainingType<Sing>();
        scanner.ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>));
        scanner.ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>));
        scanner.ConnectImplementationsToTypesClosing(typeof(IStreamRequestHandler<,>));
    });

    cfg.For<IMediator>().Use<Mediator>();
    cfg.For<TextWriter>().Use(writer);
});
```

**Key Features:**
- Fast assembly scanning
- Convention-based registration
- Built-in support for open generics

---

### 🔷 DryIoc

**📁 Location:** [`samples/MediateX.Examples.DryIoc/`](../../samples/MediateX.Examples.DryIoc/)

Lightweight and fast IoC container with zero allocations.

```csharp
var container = new Container();

container.Register<IMediator, Mediator>(Reuse.Transient);
container.RegisterMany(
    new[] { typeof(Ping).GetTypeInfo().Assembly },
    type => type.GetInterfaces()
        .Where(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
             i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))));
```

**Key Features:**
- Zero-allocation container
- Excellent performance
- Small footprint

---

### 🔷 LightInject

**📁 Location:** [`samples/MediateX.Examples.LightInject/`](../../samples/MediateX.Examples.LightInject/)

Ultra-lightweight IoC container focused on performance.

```csharp
var container = new ServiceContainer();

container.Register<IMediator, Mediator>();

// Register handlers
container.Register(typeof(IRequestHandler<,>), typeof(Ping).Assembly);
container.Register(typeof(INotificationHandler<>), typeof(Ping).Assembly);
```

**Key Features:**
- Minimal overhead
- Fast registration and resolution
- Simple API

---

### 🔷 SimpleInjector

**📁 Location:** [`samples/MediateX.Examples.SimpleInjector/`](../../samples/MediateX.Examples.SimpleInjector/)

Container with strict configuration validation and diagnostic capabilities.

```csharp
var container = new Container();

container.Register<IMediator, Mediator>(Lifestyle.Scoped);

// Register handlers
container.Collection.Register(
    typeof(IRequestHandler<,>),
    typeof(Ping).Assembly
);

container.Collection.Register(
    typeof(INotificationHandler<>),
    typeof(Ping).Assembly
);

container.Verify();
```

**Key Features:**
- Configuration validation
- Built-in diagnostics
- Lifestyle management

---

### 🔷 Stashbox

**📁 Location:** [`samples/MediateX.Examples.Stashbox/`](../../samples/MediateX.Examples.Stashbox/)

Modern IoC container with advanced features and good performance.

```csharp
var container = new StashboxContainer();

container.Register<IMediator, Mediator>();
container.RegisterAssemblyContaining<Ping>(
    config => config.WithAutoMemberInjection()
);
```

**Key Features:**
- Auto member injection
- Decorator support
- Lifetime management

---

### 🔷 Castle Windsor

**📁 Location:** [`samples/MediateX.Examples.Windsor/`](../../samples/MediateX.Examples.Windsor/)

Mature IoC container with extensive features (example in progress).

---

## Choosing a DI Container

| Container | Performance | Features | Complexity | Best For |
|-----------|------------|----------|------------|----------|
| **Microsoft.Extensions.DI** | Good | Standard | Low | ASP.NET Core, most apps |
| **Autofac** | Good | Rich | Medium | Large applications |
| **Lamar** | Excellent | Good | Medium | High-performance apps |
| **DryIoc** | Excellent | Good | Low | Performance-critical |
| **LightInject** | Excellent | Basic | Low | Microservices |
| **SimpleInjector** | Excellent | Validation | Medium | Quality-focused |
| **Stashbox** | Excellent | Modern | Medium | Modern applications |

## Recommendation

For most .NET applications, **Microsoft.Extensions.DependencyInjection** is the recommended choice as it:
- ✅ Ships with .NET
- ✅ Zero additional dependencies
- ✅ Full ASP.NET Core integration
- ✅ Well-documented and widely understood

Consider alternative containers if you need:
- Advanced features (decorators, interceptors)
- Maximum performance
- Specific configuration validation
- Legacy codebase integration

## See Also

- [Quick Start Guide](../quick-start.md)
- [ASP.NET Core Example](aspnetcore.md)
- [Publishing Strategies Example](publish-strategies.md)
