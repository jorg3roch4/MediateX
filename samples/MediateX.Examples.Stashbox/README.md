# MediateX.Examples.Stashbox

MediateX with the [Stashbox](https://github.com/z4kn4fein/stashbox) DI container.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.Stashbox
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Stashbox container setup with assembly scanning |

## Setup

```csharp
var container = new StashboxContainer()
    .RegisterInstance<TextWriter>(writer)
    .RegisterAssemblies(new[] { typeof(Mediator).Assembly, typeof(Ping).Assembly },
        serviceTypeSelector: Rules.ServiceRegistrationFilters.Interfaces, registerSelf: false);

var mediator = container.GetRequiredService<IMediator>();
```

## Notes

- Stashbox has the simplest setup of all third-party containers in these examples
- `RegisterAssemblies()` with `ServiceRegistrationFilters.Interfaces` auto-registers all interfaces
- Fluent API allows chaining registrations
- Fast and lightweight container

## Dependencies

```xml
<PackageReference Include="Stashbox" Version="6.0.0" />
<PackageReference Include="Stashbox.Extensions.DependencyInjection" Version="6.0.0" />
```
