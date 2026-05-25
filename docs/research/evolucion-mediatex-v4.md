# Investigación: Evolución MediateX v4.0
Fecha: 2025-12-29
Tarea: evolucion-mediatex
Estado: en-progreso

## Pregunta
¿Cómo evolucionar MediateX hacia v4.0 maximizando rendimiento, compatibilidad con Native AOT, y manteniendo compatibilidad hacia atrás?

---

## Resumen Ejecutivo

Esta investigación consolida el análisis previo de AOT/Source Generators y el Evolution Roadmap, añadiendo:
- Métricas actualizadas del código fuente (76 usos de reflection identificados)
- Novedades de .NET 10 LTS y C# 14 relevantes para MediateX
- Plan de implementación priorizado y actionable
- Análisis de riesgos y mitigaciones

---

## 1. Estado Actual de MediateX

### 1.1 Métricas del Código

| Componente | LOC | % | Descripción |
|------------|-----|---|-------------|
| DI | 594 | 21.9% | Inyección de dependencias |
| Registration | 569 | 20.9% | Escaneo de assemblies |
| Internal | 405 | 14.9% | TypeUnifier, ObjectDetails, HandlersOrderer |
| Behaviors | 223 | 8.2% | Pipeline behaviors |
| Wrappers | 176 | 6.5% | Dispatch dinámico |
| ExceptionHandling | 170 | 6.3% | Procesamiento de excepciones |
| Contracts | 123 | 4.5% | Interfaces marcadoras |
| Handlers | 97 | 3.6% | Interfaces de handlers |
| Core | 90 | 3.3% | IMediator, ISender, IPublisher |
| Publishing | 63 | 2.3% | Estrategias de notificaciones |
| Processing | 38 | 1.4% | Pre/Post procesadores |
| **Total** | **2,715** | 100% | |

### 1.2 Inventario de Reflection (76 ocurrencias)

#### Crítico - Mediator.cs (23 ocurrencias)
```
Línea 33-36:  Activator.CreateInstance + MakeGenericType (RequestHandler)
Línea 48-51:  Activator.CreateInstance + MakeGenericType (VoidHandler)
Línea 62-85:  GetType + GetInterfaces + GetGenericTypeDefinition (Dynamic dispatch)
Línea 119-122: Activator.CreateInstance + MakeGenericType (NotificationHandler)
Línea 133-159: Activator.CreateInstance + MakeGenericType (StreamHandler)
```

**Impacto**: 6 llamadas a `Activator.CreateInstance` - ~100x más lento que `new()` directo.

#### Crítico - ServiceRegistrar.cs (32 ocurrencias)
```
Línea 27-54:  GetDefinedTypes/GetTypes (Assembly scanning)
Línea 229-278: MakeGenericType múltiple (Cierre de genéricos)
Línea 374-417: GetInterfaces + GetGenericTypeDefinition (Contravariance fix)
Línea 535-561: GetInterfaces (Nested generic behaviors)
```

**Impacto**: Startup lento con muchos handlers. Incompatible con Native AOT.

#### Medio - TypeUnifier.cs (28 ocurrencias)
```
Línea 38-103: IsGenericType, GetGenericArguments, GetGenericTypeDefinition
Línea 145-188: GetInterfaces, MakeGenericType
```

**Propósito**: Resolver `IPipelineBehavior<TRequest, Result<T>>`. Esencial para behaviors complejos.

#### Bajo - Otros (7 ocurrencias)
- `RequestExceptionProcessorBehavior.cs`: MethodInfo.Invoke (línea 50)
- `RequestExceptionActionProcessorBehavior.cs`: MakeGenericType (líneas 72-73)
- `ObjectDetails.cs`: GetType (líneas 23-24)
- `OpenBehavior.cs`: GetInterfaces (líneas 45-46)

### 1.3 Caches Estáticos

**Ubicación**: `Mediator.cs:23-25`

```csharp
private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlers = new();
private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlers = new();
private static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> _streamRequestHandlers = new();
```

**Patrón**: GetOrAdd con lambda estática. Thread-safe pero no óptimo para lookups frecuentes.

### 1.4 Pipeline Composition

**Ubicación**: `RequestHandlerWrapper.cs:41-45`

```csharp
return serviceProvider
    .GetServices<IPipelineBehavior<TRequest, TResponse>>()
    .Reverse()
    .Aggregate((RequestHandlerDelegate<TResponse>) Handler,
        (next, pipeline) => (t) => pipeline.Handle(...))();
```

**Observación**: LINQ Aggregate crea closures en cada request. Oportunidad de optimización.

---

## 2. Contexto .NET 10 LTS (Nov 2025)

### 2.1 Mejoras Relevantes para MediateX

| Feature | Impacto | Aplicación |
|---------|---------|------------|
| **NativeAOT < 5MB** | Alto | Minimal APIs AOT ahora pesan < 5MB vs 18-25MB en .NET 8 |
| **Dynamic AOT assemblies** | Alto | Plugins y extensibilidad en AOT ahora posibles |
| **AVX10.2 + Arm64 SVE** | Medio | Vectorización para procesamiento batch |
| **GC Arm64 improvements** | Medio | 8-20% menos pause times |
| **Stack allocation arrays** | Medio | Reducción de allocations en pipeline |

### 2.2 C# 14 Features Aplicables

| Feature | Aplicación en MediateX |
|---------|------------------------|
| **Extension members** | Extension properties para IRequest metadata |
| **Field-backed properties** | Simplificar wrappers internos |
| **Implicit Span conversions** | Optimizar string handling en logging |
| **Partial constructors** | Compatibilidad con source generators |

### 2.3 Source Generators en .NET 10

- **Blazor forms**: Nuevo sistema de validación basado en source generators (AOT-compatible)
- **JsonSerializer.Context**: Auto-genera source generators en build time
- **Partial events/constructors**: Mejora compatibilidad SG ↔ código manual

---

## 3. Análisis Competitivo Actualizado

### 3.1 Matriz de Características 2025

| Feature | MediateX | MediatR | Mediator(SG) | Wolverine | Brighter |
|---------|:--------:|:-------:|:------------:|:---------:|:--------:|
| **Licencia** | Apache 2.0 | Comercial* | MIT | MIT | MIT |
| **Native AOT** | No | No | **Full** | Partial | No |
| **Source Generators** | No | No | **Yes** | No | No |
| **Streaming** | Yes | Yes | Yes | Yes | No |
| **OpenTelemetry** | No | No | No | **Built-in** | No |
| **Result<T>** | No | No | No | **Built-in** | No |
| **Retry/Circuit** | No | No | No | **Built-in** | **Yes** |
| **ValueTask** | No | No | **Yes** | Yes | No |
| **.NET 10** | Yes | Yes | Yes | Yes | Yes |

*MediatR cambió a licencia comercial en 2025

### 3.2 Benchmark de Referencia

```
┌────────────────────────────────────────────────────────────┐
│ Librería              │ Ops/sec    │ Alloc/op │ Startup   │
├───────────────────────┼────────────┼──────────┼───────────┤
│ Direct Call           │ 10,000,000 │ 0 B      │ 0 ms      │
│ Mediator (Source Gen) │  8,500,000 │ 24 B     │ 5 ms      │
│ SwitchMediator        │  7,000,000 │ 16 B     │ 3 ms      │
│ MassTransit Mediator  │    650,000 │ ~100 B   │ 50 ms     │
│ MediateX/MediatR      │    200,000 │ ~200 B   │ 100 ms    │
└────────────────────────────────────────────────────────────┘
```

**Gap**: MediateX es ~40x más lento que Mediator(SG) en throughput.

---

## 4. Arquitectura Propuesta v4.0

### 4.1 Visión: Dual-Mode Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    MediateX v4.0                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐     ┌─────────────────────────────┐   │
│  │  MediateX Core  │     │  MediateX.SourceGenerator   │   │
│  │  (Reflection)   │     │  (Compile-time)             │   │
│  │                 │     │                             │   │
│  │  - Compatible   │     │  - Native AOT               │   │
│  │  - Flexible     │     │  - Zero reflection          │   │
│  │  - Plugins OK   │     │  - ~40x faster              │   │
│  └────────┬────────┘     └──────────────┬──────────────┘   │
│           │                             │                   │
│           └──────────┬──────────────────┘                   │
│                      │                                      │
│           ┌──────────▼──────────┐                          │
│           │   IMediator API     │  ← API unificada         │
│           │   (sin cambios)     │                          │
│           └─────────────────────┘                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Source Generator - Componentes

#### Generator 1: Handler Discovery
**Reemplaza**: `ServiceRegistrar.cs` (569 LOC)

```csharp
// Input del usuario
public class GetProductHandler : IRequestHandler<GetProductQuery, Product> { }

// Output generado
public static partial class MediateXRegistration
{
    [ModuleInitializer]
    public static void RegisterHandlers()
    {
        MediateX.Generated.Handlers.Add(
            typeof(GetProductQuery),
            static sp => sp.GetRequiredService<GetProductHandler>());
    }
}
```

#### Generator 2: Dispatch Table
**Reemplaza**: `Mediator.cs` Activator.CreateInstance (23 LOC críticas)

```csharp
// Output generado
public partial class GeneratedMediator : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
    {
        return request switch
        {
            GetProductQuery q => SendTyped(q, ct),
            CreateOrderCommand c => SendTyped(c, ct),
            _ => FallbackReflection(request, ct) // Si hay modo dual
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<Product> SendTyped(GetProductQuery request, CancellationToken ct)
    {
        var handler = _sp.GetRequiredService<IRequestHandler<GetProductQuery, Product>>();
        return ExecutePipeline(request, handler, ct);
    }
}
```

#### Generator 3: Pipeline Baking
**Optimiza**: `RequestHandlerWrapper.cs:41-45`

```csharp
// Output generado - Pipeline pre-compilado
private Task<Product> ExecutePipeline_GetProductQuery(
    GetProductQuery request,
    IRequestHandler<GetProductQuery, Product> handler,
    CancellationToken ct)
{
    // Behaviors inline, sin LINQ Aggregate
    return _loggingBehavior.Handle(request,
        ct => _validationBehavior.Handle(request,
            ct => handler.Handle(request, ct), ct), ct);
}
```

### 4.3 TypeUnifier en Compile-Time

**Desafío**: Soportar `IPipelineBehavior<TRequest, Result<T>>` en source generator.

**Solución**: Emitir unificación como código generado:

```csharp
// Detectado en compile-time:
// LoggingBehavior<TRequest, TResponse> applies to GetProductQuery → Product

// Generado:
services.AddTransient<IPipelineBehavior<GetProductQuery, Product>,
    LoggingBehavior<GetProductQuery, Product>>();
```

---

## 5. Plan de Implementación Priorizado

### Fase 1: Quick Wins (v3.1.x) - Sin breaking changes

| Item | Archivo | Esfuerzo | Impacto |
|------|---------|----------|---------|
| Result<T> pattern | Nuevo: `Contracts/Result.cs` | Bajo | Alto |
| Built-in ValidationBehavior | Nuevo: `Behaviors/ValidationBehavior.cs` | Bajo | Alto |
| Built-in LoggingBehavior | Nuevo: `Behaviors/LoggingBehavior.cs` | Bajo | Medio |
| Request Correlation ID | Nuevo: `Core/IRequestContext.cs` | Bajo | Medio |
| FrozenDictionary cache | Modificar: `Mediator.cs:23-25` | Bajo | Medio |

**Tiempo estimado**: 2-4 semanas

### Fase 2: Performance (v3.2.x) - Sin breaking changes

| Item | Archivo | Esfuerzo | Impacto |
|------|---------|----------|---------|
| Compiled Expression Factory | Nuevo: `Internal/WrapperFactory.cs` | Medio | Alto |
| Eliminar LINQ en pipeline | Modificar: `Wrappers/RequestHandlerWrapper.cs:41-45` | Medio | Medio |
| ValueTask<T> handlers | Nuevo: `Handlers/IRequestHandlerAsync.cs` | Bajo | Medio |
| Span<T> optimizations | Varios | Medio | Bajo |

**Tiempo estimado**: 4-6 semanas

### Fase 3: Observability (v3.3.x) - Sin breaking changes

| Item | Archivo | Esfuerzo | Impacto |
|------|---------|----------|---------|
| OpenTelemetry Activities | Nuevo: `Telemetry/MediateXTelemetry.cs` | Medio | Alto |
| Metrics (Counter, Histogram) | Nuevo: `Telemetry/MediateXMetrics.cs` | Bajo | Medio |
| TelemetryBehavior | Nuevo: `Behaviors/TelemetryBehavior.cs` | Bajo | Alto |

**Tiempo estimado**: 2-3 semanas

### Fase 4: Source Generator (v4.0.0)

| Item | Proyecto | Esfuerzo | Impacto |
|------|----------|----------|---------|
| Crear MediateX.SourceGenerator | Nuevo proyecto | Alto | Crítico |
| Handler Discovery Generator | `MediateX.SourceGenerator/HandlerDiscovery.cs` | Alto | Crítico |
| Dispatch Table Generator | `MediateX.SourceGenerator/DispatchGenerator.cs` | Alto | Crítico |
| Pipeline Baking Generator | `MediateX.SourceGenerator/PipelineGenerator.cs` | Muy Alto | Alto |
| Compile-time Diagnostics | `MediateX.SourceGenerator/Diagnostics.cs` | Medio | Medio |
| Dual-mode fallback | Modificar: `Mediator.cs` | Medio | Alto |

**Tiempo estimado**: 8-12 semanas

### Fase 5: Resilience (v4.1.x)

| Item | Archivo | Esfuerzo | Impacto |
|------|---------|----------|---------|
| Microsoft.Extensions.Resilience | Nuevo: `Resilience/ResilienceBehavior.cs` | Medio | Alto |
| Retry policies | Nuevo: `Resilience/RetryBehavior.cs` | Bajo | Alto |
| Circuit breaker | Nuevo: `Resilience/CircuitBreakerBehavior.cs` | Medio | Alto |

**Tiempo estimado**: 3-4 semanas

---

## 6. Análisis de Riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| Complejidad de Roslyn APIs | Media | Alto | Estudiar Mediator(SG), usar IIncrementalGenerator |
| Edge cases en type unification | Alta | Medio | Tests exhaustivos, fuzzing de tipos |
| Breaking changes accidentales | Media | Alto | Suite de compatibilidad, semantic versioning |
| Performance del generador | Baja | Bajo | Incremental generators son eficientes |
| Adopción lenta de SG | Media | Medio | Mantener modo reflection como default |

---

## 7. Decisiones Técnicas Pendientes

### Alta Prioridad
- [ ] ¿Source generator como paquete separado o integrado?
  - **Recomendación**: Separado (`MediateX.SourceGenerator`) para opt-in explícito
- [ ] ¿Mantener reflection como fallback en modo SG?
  - **Recomendación**: Sí, para plugins/assemblies dinámicos
- [ ] ¿Mínimo .NET para source generator?
  - **Recomendación**: .NET 8+ (LTS anterior) con full support en .NET 10

### Media Prioridad
- [ ] ¿Result<T> como tipo propio o OneOf/LanguageExt?
  - **Recomendación**: Tipo propio simple, sin dependencias
- [ ] ¿OpenTelemetry como dependencia directa?
  - **Recomendación**: Abstractions solamente, implementación opt-in

---

## 8. Métricas de Éxito

| Métrica | Actual | v3.2 Target | v4.0 Target |
|---------|:------:|:-----------:|:-----------:|
| Handler dispatch (ops/sec) | 200K | 500K | 5M+ |
| Memory per request | ~200B | ~100B | ~24B |
| Startup time (1000 handlers) | 100ms | 50ms | 5ms |
| Native AOT support | No | No | **Yes** |
| Reflection calls at runtime | 76 | 50 | **0** (SG mode) |
| Test coverage | ~70% | 85% | 95% |

---

## 9. Fuentes

### Documentos Base
- `temp/AOT-SourceGenerators-Investigation.md` - Investigación AOT previa (2025-12-25)
- `temp/EVOLUTION-ROADMAP.md` - Roadmap de evolución

### Análisis de Código
- `src/MediateX/Mediator.cs` - 167 LOC, 23 reflection calls
- `src/MediateX/Registration/ServiceRegistrar.cs` - 569 LOC, 32 reflection calls
- `src/MediateX/Internal/TypeUnifier.cs` - 196 LOC, 28 reflection calls

### Referencias Externas
- [.NET 10 Announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [What's new in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Mediator Source Generator](https://github.com/martinothamar/Mediator)
- [SwitchMediator Benchmarks](https://github.com/zachsaw/SwitchMediator)

---

## 10. Próximos Pasos

1. **Inmediato**: Implementar Result<T> y ValidationBehavior (v3.1)
2. **Corto plazo**: Crear spike de source generator para validar arquitectura
3. **Medio plazo**: Implementar WrapperFactory con compiled expressions
4. **Largo plazo**: Release de MediateX.SourceGenerator con Native AOT

---

## Conclusiones

MediateX tiene una oportunidad única de diferenciación:

1. **MediatR es ahora comercial** - MediateX es la alternativa open-source líder
2. **.NET 10 mejora Native AOT** - El ecosistema está listo para source generators
3. **Gap de rendimiento es cerrable** - Con SG se puede alcanzar ~40x mejora

La estrategia dual-mode permite:
- **Compatibilidad total** con código existente (reflection mode)
- **Máximo rendimiento** para nuevos proyectos (SG mode)
- **Native AOT** para serverless/edge computing

---

*Documento consolidado: 2025-12-29*
*Basado en: AOT-SourceGenerators-Investigation.md, EVOLUTION-ROADMAP.md*
*Estado: En progreso - Pendiente validación con spike*
