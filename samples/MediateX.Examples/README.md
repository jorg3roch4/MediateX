# MediateX.Examples

Shared library containing all core MediateX patterns used by the other samples.

## Purpose

This is not a runnable project. It's a class library that provides:

- Request/Response patterns (`Ping`/`Pong`)
- Notifications (`Pinged`)
- Streaming (`Sing`/`Song`)
- Exception handling examples
- Pipeline behaviors
- Pre/Post processors

## Key Files

| File | Description |
|------|-------------|
| `Ping.cs` | Basic request implementing `IRequest<Pong>` |
| `PingHandler.cs` | Handler for `Ping` requests |
| `Pinged.cs` | Notification implementing `INotification` |
| `PingedHandler.cs` / `PingedAlsoHandler.cs` | Multiple notification handlers |
| `Runner.cs` | Test orchestrator that validates all MediateX features |
| `GenericPipelineBehavior.cs` | Open generic pipeline behavior |
| `GenericRequestPreProcessor.cs` | Pre-processor example |
| `GenericRequestPostProcessor.cs` | Post-processor example |
| `ConstrainedRequestPostProcessor.cs` | Generic post-processor with type constraints |

### Streaming

| File | Description |
|------|-------------|
| `Streams/Sing.cs` | Stream request implementing `IStreamRequest<Song>` |
| `Streams/SingHandler.cs` | Handler returning `IAsyncEnumerable<Song>` |

### Exception Handling

| File | Description |
|------|-------------|
| `ExceptionHandler/` | Folder with exception request/response patterns |

## Usage

Other samples reference this project:

```xml
<ProjectReference Include="..\MediateX.Examples\MediateX.Examples.csproj" />
```

Then register handlers from this assembly:

```csharp
cfg.RegisterServicesFromAssemblyContaining<Ping>();
```
