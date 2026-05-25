# MediateX — Agent Context

## Project

Pure mediator library for .NET 10+. In-process messaging with request/response, notifications, streaming, and pipeline behaviors. Based on MediatR 12.5.0, ~2,700 lines of code, 1 dependency. Published to NuGet.

## Solution: MediateX.slnx

### Source packages (src/)
- **MediateX** — Core mediator: contracts, handlers, pipeline behaviors, publishing, DI registration

### Tests (test/)
- MediateX.Tests — 168 unit tests covering all messaging patterns
- MediateX.Benchmarks — BenchmarkDotNet performance benchmarks

### Samples (samples/)
- MediateX.Examples — Console app: basic request/response/notifications
- MediateX.Examples.AspNetCore — ASP.NET Core integration
- MediateX.Examples.Autofac — Autofac DI container
- MediateX.Examples.PublishStrategies — Custom notification publish strategies
- MediateX.Examples.Stashbox — Stashbox DI container

## Source Structure (src/MediateX/)

- **Abstractions/** — Public contracts: IRequest, INotification, IStreamRequest, IMediator
- **Behaviors/** — IPipelineBehavior, IRequestExceptionHandler pipeline
- **Contracts/** — Core interfaces
- **Core/** — Mediator implementation
- **DI/** — AddMediateX(), RegisterServicesFromAssemblyContaining
- **ExceptionHandling/** — IRequestExceptionHandler with recovery
- **Handlers/** — IRequestHandler, ISyncRequestHandler, INotificationHandler
- **Processing/** — Request dispatch, pipeline execution
- **Publishing/** — Notification fan-out strategies
- **Registration/** — Assembly scanning, handler registration
- **Wrappers/** — Internal handler wrappers

## Stack

- .NET 10 / C# 14, nullable enabled, warnings as errors, strict features
- Single dependency: Microsoft.Extensions.DependencyInjection.Abstractions
- v4.0 added ISyncRequestHandler<,> and ISyncNotificationHandler<> with zero-allocation cache hits
- InternalsVisibleTo: MediateX.Tests, MediateX.Benchmarks
