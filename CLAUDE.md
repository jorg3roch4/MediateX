# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MediateX is a modern mediator implementation for .NET 9+ for in-process messaging with no dependencies. It supports request/response, commands, queries, notifications and events, both synchronous and async with intelligent dispatching via C# generic variance.

**Note:** MediateX 1.0.0 is a fork of MediatR 12.5.0, modernized for .NET 9 and maintained as an independent project.

### Technology Stack
- **Target Framework**: .NET 9.0 (LTS)
- **C# Version**: Latest (C# 13)
- **No .NET Framework support** - Modern .NET only

## Build and Test Commands

### Building the project
```powershell
.\Build.ps1
```
This script:
- Cleans the solution (`dotnet clean -c Release`)
- Builds in Release mode (`dotnet build -c Release`)
- Runs all tests (`dotnet test -c Release --no-build -l trx --verbosity=normal`)
- Packages the main MediateX library to `.\artifacts` directory

### Building MediateX.Contracts only
```powershell
.\BuildContracts.ps1
```
Builds and packages only the MediateX.Contracts project.

### Running tests
```bash
dotnet test -c Release
```

### Running a single test
```bash
dotnet test --filter "FullyQualifiedName~TestNamespace.TestClass.TestMethod"
```
Or by display name:
```bash
dotnet test --filter "DisplayName~TestName"
```

### Packaging
```bash
dotnet pack .\src\MediateX\MediateX.csproj -c Release -o .\artifacts
```

## Architecture

### Core Interfaces

MediateX uses a mediator pattern with three primary interfaces:

- **`IMediator`** - Main interface that combines `ISender` and `IPublisher`
- **`ISender`** - For sending requests to a single handler (request/response pattern)
- **`IPublisher`** - For publishing notifications to multiple handlers (pub/sub pattern)

### Request/Response Flow

1. **Request Types**:
   - `IRequest<TResponse>` - Requests with a response
   - `IRequest` - Requests with no response (void, returns `Unit`)
   - `IStreamRequest<TResponse>` - Requests that return an async stream

2. **Handler Resolution**: When a request is sent via `ISender.Send()`:
   - The appropriate `RequestHandlerWrapper` is selected based on the request type
   - The wrapper uses `IServiceProvider` to resolve the correct `IRequestHandler<TRequest, TResponse>`
   - Pipeline behaviors are retrieved and chained in reverse order using `Aggregate()`
   - The handler is invoked through the behavior pipeline

3. **Pipeline Execution**: Behaviors wrap around the handler in this pattern:
   ```
   Request → Behavior1 → Behavior2 → ... → Handler → Response
   ```
   Pipeline behaviors (`IPipelineBehavior<TRequest, TResponse>`) are executed in reverse registration order and must call the `next` delegate to continue the pipeline.

### Notification/Event Flow

1. **Notification Types**:
   - `INotification` - Base interface for notifications
   - Multiple `INotificationHandler<TNotification>` implementations can handle the same notification

2. **Publishing Strategies**: Controlled via `INotificationPublisher`:
   - **`ForeachAwaitPublisher`** - Sequential execution (awaits each handler in a foreach loop)
   - **`TaskWhenAllPublisher`** - Concurrent execution (uses `Task.WhenAll()`)

3. **Handler Ordering**: The `HandlersOrderer` class in `src/MediatR/Internal/HandlersOrderer.cs` prioritizes handlers:
   - Removes overridden handlers (when a derived handler overrides a base handler)
   - Sorts handlers based on type hierarchy relative to the notification type
   - More specific handlers execute before more general handlers

### Dependency Injection

MediatR integrates with `Microsoft.Extensions.DependencyInjection` via `ServiceCollectionExtensions.AddMediatR()`:

- Scans assemblies for handler implementations
- Registers handlers as transient
- Supports open generic registrations for `INotificationHandler<>`, `IRequestExceptionHandler<,,>`, and `IRequestExceptionAction<,>`
- Behaviors, stream behaviors, and pre/post processors must be registered explicitly using configuration methods like `AddBehavior<T>()`, `AddOpenBehavior(typeof(T<,>))`, etc.

### Key Extension Points

1. **Pipeline Behaviors** (`IPipelineBehavior<TRequest, TResponse>`):
   - Wrap around request handlers
   - Execute in reverse registration order
   - Common uses: logging, validation, transaction management, caching

2. **Pre/Post Processors**:
   - `IRequestPreProcessor<TRequest>` - Executed before the handler via `RequestPreProcessorBehavior`
   - `IRequestPostProcessor<TRequest, TResponse>` - Executed after the handler via `RequestPostProcessorBehavior`

3. **Exception Handling**:
   - `IRequestExceptionHandler<TRequest, TResponse, TException>` - Handle specific exceptions for specific request/response types
   - `IRequestExceptionAction<TRequest, TException>` - Execute actions when exceptions occur
   - Processed via `RequestExceptionProcessorBehavior` and `RequestExceptionActionProcessorBehavior`

4. **Stream Behaviors** (`IStreamPipelineBehavior<TRequest, TResponse>`):
   - Wrap around stream request handlers
   - Similar to pipeline behaviors but for async streams

## Project Structure

- **`src/MediateX`** - Main library implementation
  - `Wrappers/` - Internal wrapper classes for dynamic dispatch (`RequestHandlerWrapper`, `NotificationHandlerWrapper`, `StreamRequestHandlerWrapper`)
  - `Pipeline/` - Built-in pipeline behaviors for pre/post processing and exception handling
  - `MicrosoftExtensionsDI/` - DI integration for Microsoft.Extensions.DependencyInjection
  - `NotificationPublishers/` - Publishing strategy implementations
  - `Internal/` - Internal utilities like `HandlersOrderer` for handler prioritization

- **`src/MediateX.Contracts`** - Lightweight contracts-only package containing `IRequest`, `INotification`, `IStreamRequest`, and `Unit`

- **`test/MediateX.Tests`** - xUnit tests using Shouldly for assertions

- **`samples/`** - Example integrations with various DI containers (AspNetCore, Autofac, DryIoc, Lamar, LightInject, SimpleInjector, Stashbox, Windsor)

## Development Notes

- **Version**: 1.0.0
- **Target Framework**: .NET 9.0 (all projects)
- **C# Version**: Latest (C# 13) - see `Directory.Build.props`
- **Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 9.0.0
- **Warnings as Errors**: Enabled via `TreatWarningsAsErrors` in `Directory.Build.props`
- **Testing Framework**: xUnit with Shouldly assertions and Lamar for DI testing
- **Based on**: MediatR 12.5.0, modernized for .NET 9

### .NET 9 Features
- Optimized for .NET 9 performance improvements
- Uses latest C# 13 language features
- Leverages .NET 9 BCL enhancements
- No legacy .NET Framework or .NET Standard support
