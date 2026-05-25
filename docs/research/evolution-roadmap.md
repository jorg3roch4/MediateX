# MediateX Evolution Roadmap

Este documento presenta un análisis exhaustivo del patrón Mediator y propuestas de mejora para MediateX, manteniendo compatibilidad hacia atrás.

---

## 1. Análisis del Patrón Mediator

### 1.1 Origen (Gang of Four)

El patrón Mediator es un patrón de comportamiento que **encapsula cómo interactúan un conjunto de objetos**. Promueve el acoplamiento débil evitando que los objetos se refieran entre sí explícitamente.

```
┌─────────────────────────────────────────────────────────────┐
│                    Patrón Clásico GoF                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌──────────┐      ┌──────────┐      ┌──────────┐        │
│   │Colleague1│      │ Mediator │      │Colleague2│        │
│   └────┬─────┘      └────┬─────┘      └────┬─────┘        │
│        │                 │                 │               │
│        │    notify()     │                 │               │
│        │────────────────>│                 │               │
│        │                 │   update()      │               │
│        │                 │────────────────>│               │
│        │                 │                 │               │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Evolución Moderna en .NET

MediateX implementa una evolución del patrón como **Message Bus en proceso**:

```
┌─────────────────────────────────────────────────────────────┐
│                  Implementación MediateX                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Controller ──> IRequest ──> IMediator ──> Pipeline ──> Handler
│                                  │                          │
│                                  ├── Behaviors (logging)    │
│                                  ├── Behaviors (validation) │
│                                  └── Behaviors (caching)    │
│                                                             │
│  Características:                                           │
│  • Request/Response con handler único                       │
│  • Notifications con múltiples handlers                     │
│  • Streaming con IAsyncEnumerable<T>                       │
│  • Pipeline behaviors para cross-cutting concerns           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Comparativa con Librerías Competidoras

### 2.1 Matriz de Características

| Feature | MediateX | MediatR | Mediator (SG) | Wolverine | Brighter |
|---------|:--------:|:-------:|:-------------:|:---------:|:--------:|
| **Enfoque** | Reflection | Reflection | Source Gen | Code Gen | Attributes |
| **Native AOT** | Limited | Limited | **Full** | Partial | Limited |
| **Streaming** | Yes | Yes | Yes | Yes | No |
| **Transactional Outbox** | No | No | No | **Yes** | **Yes** |
| **Sagas/State Machine** | No | No | No | **Yes** | No |
| **OpenTelemetry Built-in** | No | No | No | **Yes** | No |
| **Parallel Notifications** | Yes | Yes | Yes | Yes | N/A |
| **Convention Discovery** | No | No | No | **Yes** | No |
| **Attribute Pipeline** | No | No | No | No | **Yes** |
| **Retry/Circuit Breaker** | No | No | No | **Yes** | **Yes** |
| **ValueTask Returns** | No | No | **Yes** | Yes | No |
| **Compile-time Diagnostics** | No | No | **Yes** | No | No |

### 2.2 Benchmark de Rendimiento (Referencia)

```
┌────────────────────────────────────────────────────────────┐
│ Librería              │ Ops/sec    │ Alloc/op │ Startup   │
├───────────────────────┼────────────┼──────────┼───────────┤
│ Direct Call           │ 10,000,000 │ 0 B      │ 0 ms      │
│ Mediator (Source Gen) │  8,500,000 │ 24 B     │ 5 ms      │
│ MassTransit Mediator  │    650,000 │ ~100 B   │ 50 ms     │
│ MediatR/MediateX      │    200,000 │ ~200 B   │ 100 ms    │
└────────────────────────────────────────────────────────────┘
* Nota: Benchmarks aproximados, varían según escenario
```

---

## 3. Análisis de MediateX - Áreas de Mejora

### 3.1 Problemas de Rendimiento Identificados

#### A. Uso Excesivo de Activator.CreateInstance
**Ubicación:** `src/MediateX/Mediator.cs` (líneas 36, 51, 83, 122, 136, 159)

```csharp
// Actual - 6 llamadas a Activator.CreateInstance
var wrapper = (RequestHandlerWrapperImpl<TRequest, TResponse>)
    Activator.CreateInstance(typeof(RequestHandlerWrapperImpl<,>)
        .MakeGenericType(requestType, typeof(TResponse)))!;
```

**Impacto:** ~100x más lento que `new()` directo.

#### B. Explosión de Registros Genéricos
**Ubicación:** `src/MediateX/Registration/ServiceRegistrar.cs` (líneas 255-325)

```csharp
// 5 parámetros genéricos × 10 tipos cada uno = 100,000 registros
long totalCombinations = 1;
foreach (var list in lists)
    totalCombinations *= list.Count;
```

**Impacto:** Startup lento, alto consumo de memoria.

#### C. Algoritmo O(n²) en HandlersOrderer
**Ubicación:** `src/MediateX/Internal/HandlersOrderer.cs` (líneas 26-49)

```csharp
// Nested loops para detectar overrides
for (var i = 0; i < handlersData.Count - 1; i++)
    for (var j = i + 1; j < handlersData.Count; j++)
        if (handlersData[i].Type.IsAssignableFrom(handlersData[j].Type))
```

**Impacto:** Con 100 handlers = 10,000 comparaciones de tipos.

### 3.2 Características Faltantes

| Categoría | Feature | Prioridad | Complejidad |
|-----------|---------|:---------:|:-----------:|
| Performance | Source Generator alternativo | Alta | Alta |
| Performance | ValueTask<T> returns | Media | Baja |
| Reliability | Result<T> pattern | Alta | Media |
| Reliability | Retry/Circuit Breaker | Media | Media |
| Observability | OpenTelemetry integrado | Alta | Media |
| Observability | Request correlation IDs | Media | Baja |
| DX | Compile-time diagnostics | Media | Alta |
| DX | Validation pipeline built-in | Alta | Baja |
| Advanced | Transactional Outbox | Baja | Alta |
| Advanced | Saga support | Baja | Alta |

### 3.3 Deuda Técnica

1. **Silent catch blocks** en `ServiceRegistrar.cs` (líneas 227-233)
2. **Estado mutable** en `ObjectDetails.cs` durante sorting
3. **Sin validación** de configuración en `MediateXServiceConfiguration.cs`
4. **Caches estáticos** impiden aislamiento en tests

---

## 4. Propuestas de Mejora (Sin Breaking Changes)

### 4.1 Fase 1: Quick Wins (v3.1.x)

#### 4.1.1 Result<T> Pattern Support

```csharp
// NUEVO: Tipo Result<T> built-in
namespace MediateX;

public readonly struct Result<T>
{
    public T? Value { get; }
    public Exception? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Exception error) => new(default, error);
}

// NUEVO: Interface opcional para handlers que retornan Result
public interface IResultRequest<T> : IRequest<Result<T>> { }
```

**Compatibilidad:** 100% - Es aditivo, no modifica APIs existentes.

#### 4.1.2 Request Correlation ID

```csharp
// NUEVO: Contexto de request propagable
public interface IRequestContext
{
    string RequestId { get; }
    string? CorrelationId { get; }
    IDictionary<string, object?> Items { get; }
}

// NUEVO: Behavior que inyecta contexto
public class RequestContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity(typeof(TRequest).Name);
        activity?.SetTag("request.id", Guid.NewGuid().ToString());
        return await next(ct);
    }
}
```

**Compatibilidad:** 100% - Opt-in via configuración.

#### 4.1.3 Built-in Validation Behavior

```csharp
// NUEVO: Interface de validación
public interface IRequestValidator<in TRequest>
{
    ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken ct);
}

public record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors);
public record ValidationError(string PropertyName, string ErrorMessage);

// NUEVO: Behavior de validación
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IRequestValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = new List<ValidationError>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, ct);
            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(ct);
    }
}
```

**Compatibilidad:** 100% - Opt-in, no afecta usuarios existentes.

#### 4.1.4 Logging Behavior Built-in

```csharp
// NUEVO: Behavior de logging configurable
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(ct);
            _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestName}", requestName);
            throw;
        }
    }
}
```

**Compatibilidad:** 100% - Opt-in behavior.

---

### 4.2 Fase 2: Performance Improvements (v3.2.x)

#### 4.2.1 Compiled Expression Wrapper Factory

```csharp
// NUEVO: Reemplazar Activator.CreateInstance con expresiones compiladas
internal static class WrapperFactory
{
    private static readonly ConcurrentDictionary<Type, Func<object>> _factories = new();

    public static T Create<T>() where T : class
    {
        var factory = _factories.GetOrAdd(typeof(T), static type =>
        {
            var ctor = type.GetConstructor(Type.EmptyTypes)!;
            var newExpr = Expression.New(ctor);
            var lambda = Expression.Lambda<Func<object>>(newExpr);
            return lambda.Compile();
        });

        return (T)factory();
    }
}
```

**Impacto esperado:** ~10x mejora en creación de wrappers.
**Compatibilidad:** 100% - Cambio interno de implementación.

#### 4.2.2 ValueTask Support (Opcional)

```csharp
// NUEVO: Interface alternativa con ValueTask
public interface IRequestHandlerAsync<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct);
}

// El Mediator detecta y usa la versión apropiada
```

**Compatibilidad:** 100% - Nueva interface opcional, las existentes siguen funcionando.

#### 4.2.3 FrozenDictionary para Handler Cache

```csharp
// NUEVO: Usar FrozenDictionary para caches inmutables después de warmup
public sealed class Mediator : IMediator
{
    // Warmup phase usa ConcurrentDictionary
    private static ConcurrentDictionary<Type, RequestHandlerBase>? _mutableCache = new();

    // Después de warmup, se congela para mejor rendimiento
    private static FrozenDictionary<Type, RequestHandlerBase>? _frozenCache;

    public static void FreezeCache()
    {
        if (_mutableCache is not null)
        {
            _frozenCache = _mutableCache.ToFrozenDictionary();
            _mutableCache = null;
        }
    }
}
```

**Impacto esperado:** ~30% mejora en lookups después de warmup.
**Compatibilidad:** 100% - Opt-in via `Mediator.FreezeCache()`.

---

### 4.3 Fase 3: Observability (v3.3.x)

#### 4.3.1 OpenTelemetry Integration

```csharp
// NUEVO: Soporte nativo de OpenTelemetry
public static class MediateXTelemetry
{
    public static readonly ActivitySource ActivitySource = new("MediateX");

    public static readonly Meter Meter = new("MediateX");

    private static readonly Counter<long> RequestCounter =
        Meter.CreateCounter<long>("mediatex.requests.count");

    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("mediatex.requests.duration", "ms");
}

// NUEVO: Behavior de telemetría
public class TelemetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        using var activity = MediateXTelemetry.ActivitySource
            .StartActivity($"MediateX.{typeof(TRequest).Name}");

        activity?.SetTag("mediatex.request.type", typeof(TRequest).FullName);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(ct);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            MediateXTelemetry.RequestCounter.Add(1,
                new KeyValuePair<string, object?>("request.type", typeof(TRequest).Name));
            MediateXTelemetry.RequestDuration.Record(sw.Elapsed.TotalMilliseconds);
        }
    }
}
```

**Compatibilidad:** 100% - Opt-in via configuración.

#### 4.3.2 Configuración de Telemetría

```csharp
// Extensión de configuración
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // NUEVO: Habilitar telemetría
    cfg.EnableTelemetry(telemetry =>
    {
        telemetry.EnableTracing = true;
        telemetry.EnableMetrics = true;
        telemetry.ActivitySourceName = "MyApp.MediateX";
    });
});
```

---

### 4.4 Fase 4: Source Generator (v4.0.0)

#### 4.4.1 Paquete Opcional de Source Generation

```xml
<!-- Nuevo paquete opcional -->
<PackageReference Include="MediateX.SourceGenerator" Version="4.0.0" />
```

```csharp
// Genera código en compile-time
[assembly: MediateXAssembly]

// El source generator crea:
// - Dispatch optimizado sin reflection
// - Registros de DI directos
// - Diagnósticos de compile-time
```

**Compatibilidad:**
- MediateX base sigue funcionando con reflection
- Source generator es **opt-in** para quienes necesiten Native AOT o máximo rendimiento

#### 4.4.2 Diagnósticos de Compile-Time

```csharp
// El source generator emite warnings/errors:
// MEDX001: Handler not found for request type 'MyRequest'
// MEDX002: Multiple handlers found for request type 'MyRequest'
// MEDX003: Handler 'MyHandler' has async void method (should be Task)
```

---

### 4.5 Fase 5: Resilience (v4.1.x)

#### 4.5.1 Integración con Microsoft.Extensions.Resilience

```csharp
// NUEVO: Soporte de políticas de resiliencia
public interface IResilientRequest<TResponse> : IRequest<TResponse>
{
    string ResiliencePipelineName { get; }
}

// NUEVO: Behavior de resiliencia
public class ResilienceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IResilientRequest<TResponse>
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var pipeline = _pipelineProvider.GetPipeline(request.ResiliencePipelineName);
        return await pipeline.ExecuteAsync(async token => await next(token), ct);
    }
}
```

#### 4.5.2 Configuración de Resiliencia

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // NUEVO: Configurar resiliencia por tipo de request
    cfg.AddResiliencePipeline<MyCommand>("my-command-pipeline", builder =>
    {
        builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30)
            });
    });
});
```

---

## 5. Plan de Releases

```
┌─────────────────────────────────────────────────────────────────┐
│                    MediateX Evolution Timeline                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  v3.1.0 ─────────────────────────────────────────────> Q1 2026 │
│  ├── Result<T> pattern support                                 │
│  ├── Built-in ValidationBehavior                               │
│  ├── Built-in LoggingBehavior                                  │
│  └── Request correlation IDs                                   │
│                                                                 │
│  v3.2.0 ─────────────────────────────────────────────> Q2 2026 │
│  ├── Compiled expression wrapper factory                       │
│  ├── ValueTask<T> optional handlers                            │
│  ├── FrozenDictionary cache optimization                       │
│  └── Performance benchmarks in CI                              │
│                                                                 │
│  v3.3.0 ─────────────────────────────────────────────> Q3 2026 │
│  ├── OpenTelemetry integration                                 │
│  ├── Built-in metrics (requests, duration, errors)             │
│  └── Distributed tracing support                               │
│                                                                 │
│  v4.0.0 ─────────────────────────────────────────────> Q4 2026 │
│  ├── MediateX.SourceGenerator package (opt-in)                 │
│  ├── Native AOT support via source generation                  │
│  ├── Compile-time diagnostics                                  │
│  └── Zero-reflection dispatch mode                             │
│                                                                 │
│  v4.1.0 ─────────────────────────────────────────────> Q1 2027 │
│  ├── Microsoft.Extensions.Resilience integration               │
│  ├── Retry/Circuit breaker behaviors                           │
│  └── Timeout policies                                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 6. Garantías de Compatibilidad

### 6.1 Principios

1. **Todas las APIs públicas existentes permanecen sin cambios**
2. **Nuevas features son opt-in** via configuración o nuevos paquetes
3. **Comportamiento por defecto idéntico** al actual
4. **Semantic versioning estricto** (breaking changes solo en major versions)

### 6.2 Estrategia de Deprecación

```csharp
// Ejemplo de deprecación gradual (si fuera necesario)
[Obsolete("Use IResultRequest<T> instead. Will be removed in v5.0")]
public interface IOldInterface { }
```

### 6.3 Testing de Compatibilidad

```csharp
// Agregar suite de tests de compatibilidad
[Fact]
public async Task Existing_Handlers_Continue_Working_After_Upgrade()
{
    // Verificar que código existente funciona sin modificaciones
}
```

---

## 7. Métricas de Éxito

| Métrica | Actual | v3.2 Target | v4.0 Target |
|---------|:------:|:-----------:|:-----------:|
| Handler dispatch (ops/sec) | 200K | 500K | 2M+ |
| Memory per request | ~200B | ~100B | ~24B |
| Startup time (1000 handlers) | 100ms | 50ms | 10ms |
| Native AOT support | No | No | Yes |
| Test coverage | ~70% | 85% | 95% |

---

## 8. Conclusión

MediateX tiene una base sólida pero puede evolucionar significativamente adoptando:

1. **Patrones modernos** (Result<T>, Validation, Telemetry)
2. **Optimizaciones de rendimiento** (Compiled expressions, FrozenDictionary)
3. **Source generation opcional** para escenarios de alto rendimiento
4. **Integración con el ecosistema** (OpenTelemetry, Resilience)

Todo esto manteniendo **100% de compatibilidad hacia atrás** para usuarios existentes.
