# MediateX: Investigación Native AOT con Source Generators

**Fecha:** 2025-12-25
**Propósito:** Evaluar la viabilidad e implementación de Native AOT para MediateX
**Estado:** Investigación completada - Pendiente decisión de roadmap

---

## 1. Contexto: ¿Qué es Native AOT?

### Compilación Tradicional (JIT)
```
Código C# → IL (Intermediate Language) → [Runtime] → Código máquina
                                              ↑
                                         JIT Compiler
```

### Compilación AOT (Ahead-of-Time)
```
Código C# → IL → [Build time] → Ejecutable nativo
                       ↑
                  AOT Compiler
```

### Beneficios de Native AOT

| Aspecto | JIT | Native AOT | Mejora |
|---------|-----|------------|--------|
| Tiempo de startup | ~500ms | ~5ms | **100x más rápido** |
| Memoria inicial | ~50MB | ~10MB | **5x menos** |
| Tamaño de deploy | Grande (incluye runtime) | Pequeño | **Significativo** |
| Cold start (serverless) | Problema | Resuelto | **Crítico para cloud** |

### Limitaciones de Native AOT

- **Reflection:** Limitado o no funciona
- **Assembly scanning:** No funciona
- **Type.MakeGenericType:** No funciona en runtime
- **Activator.CreateInstance:** No funciona
- **Dynamic code generation:** No funciona

---

## 2. Estado del Ecosistema - Mediadores con AOT

### Librerías Existentes con Source Generators

#### 2.1 Mediator (martinothamar)
- **GitHub:** https://github.com/martinothamar/Mediator
- **Características:**
  - API similar a MediatR
  - Source generators completos
  - Native AOT compatible
  - .NET Standard 2.0 y .NET 8+
- **Arquitectura:**
  - Generación de código en compile-time
  - Implementación monomorfa de IMediator
  - Diccionarios de búsqueda compilados
  - Sin reflection en runtime

#### 2.2 SwitchMediator
- **GitHub:** https://github.com/zachsaw/SwitchMediator
- **Benchmarks:**
  - **1688x más rápido** que MediatR
  - **117x menos memoria** al inicio
- **Arquitectura:**
  - Genera switch statements explícitos
  - Dispatch directo sin reflection
  - Código completamente estático

#### 2.3 MinimalMediator
- Native AOT compatible
- Sin serialización ni reflection
- Basado en System.Threading.Channels

#### 2.4 MediatorSourceGenerator (murunu)
- NativeAOT compatible
- Simple pero funcional

### Observación Importante
MediatR (la librería de referencia) cambió a **licencia comercial en 2025**. No tiene soporte nativo a AOT.

---

## 3. Análisis del Código Actual de MediateX

### 3.1 Uso de Reflection - Inventario Completo

**Total: 91 ocurrencias en 9 archivos**

#### Archivo: `Mediator.cs` (24 ocurrencias) - CRÍTICO

```csharp
// Línea 33-36: Creación dinámica de wrappers
var handler = (RequestHandlerWrapper<TResponse>)Activator.CreateInstance(
    typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse)))!;

// Línea 48-51: IRequest sin TResponse
var handler = (RequestHandlerWrapper)Activator.CreateInstance(
    typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType))!;

// Línea 62-85: Inspección de tipos en runtime
request.GetType()
GetInterfaces()
GetGenericTypeDefinition()

// Línea 121: NotificationHandlerWrapper
Activator.CreateInstance(typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(...))

// Línea 135-136: StreamRequestHandler
Activator.CreateInstance(typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(...))
```

**Problemas AOT:**
- `Activator.CreateInstance` es fundamentalmente incompatible
- Los wrappers se crean dinámicamente según el tipo del request
- No se conoce en compile-time qué requests usará la aplicación

#### Archivo: `ServiceRegistrar.cs` (23 ocurrencias) - CRÍTICO

```csharp
// Línea 27-37: Escaneo de ensamblados
internal static IEnumerable<TypeInfo> GetLoadableDefinedTypes(Assembly assembly)
{
    return assembly.DefinedTypes;  // NO AOT COMPATIBLE
}

// Línea 44-54: GetTypes
internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
{
    return assembly.GetTypes();  // NO AOT COMPATIBLE
}

// Línea 129-212: ConnectImplementationsToTypesClosing
SelectMany(GetLoadableDefinedTypes)
FindInterfacesThatClose()  // reflection recursiva
ContainsGenericParameters
IsOpenGeneric()
GetGenericArguments()

// Línea 229, 250, 278: MakeGenericType
Type.MakeGenericType(closingTypes)  // NO AOT COMPATIBLE
```

#### Archivo: `TypeUnifier.cs` (13 ocurrencias) - MEDIO

```csharp
// Type unification para behaviors con generics anidados
IsGenericType
IsGenericParameter
GetGenericArguments()
GetGenericTypeDefinition()
```

#### Archivo: `HandlersOrderer.cs` (2 ocurrencias) - BAJO

```csharp
s.GetType()
type.IsAssignableFrom()
```

#### Otros archivos con reflection menor:
- `MediateXServiceConfiguration.cs`: 4 ocurrencias
- `ObjectDetails.cs`: reflection menor
- `RequestExceptionProcessorBehavior.cs`: reflection menor
- `RequestExceptionActionProcessorBehavior.cs`: reflection menor
- `OpenBehavior.cs`: 1 ocurrencia

### 3.2 Resumen de Impacto

| Categoría | Ubicación | Impacto | Prioridad |
|-----------|-----------|---------|-----------|
| Wrapper Creation | Mediator.cs | Runtime dinámico | **P0** |
| Assembly Scanning | ServiceRegistrar.cs | Startup discovery | **P0** |
| Type Unification | TypeUnifier.cs | Nested generics | **P1** |
| Handler Ordering | HandlersOrderer.cs | Runtime dispatch | **P2** |

---

## 4. Arquitectura Propuesta con Source Generators

### 4.1 Estructura del Proyecto

```
MediateX/
├── src/
│   ├── MediateX/                      # Librería principal (modificada)
│   └── MediateX.SourceGenerator/      # NUEVO: Source generator
├── test/
│   ├── MediateX.Tests/
│   └── MediateX.SourceGenerator.Tests/ # NUEVO: Tests del generador
```

### 4.2 Componentes del Source Generator

#### Generador 1: Handler Discovery
Reemplaza `ServiceRegistrar.cs`

```csharp
// Input: Código del usuario
public class GetProductHandler : IRequestHandler<GetProductQuery, Product> { }
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Order> { }

// Output generado:
public static class MediateXRegistration
{
    public static IServiceCollection AddMediateXHandlers(this IServiceCollection services)
    {
        services.AddTransient<IRequestHandler<GetProductQuery, Product>, GetProductHandler>();
        services.AddTransient<IRequestHandler<CreateOrderCommand, Order>, CreateOrderHandler>();
        return services;
    }
}
```

#### Generador 2: Wrapper Generation
Reemplaza `Activator.CreateInstance` en `Mediator.cs`

```csharp
// Output generado:
public static class MediateXWrappers
{
    public static RequestHandlerWrapper<Product> GetWrapper_GetProductQuery()
        => new RequestHandlerWrapperImpl<GetProductQuery, Product>();

    public static RequestHandlerWrapper<Order> GetWrapper_CreateOrderCommand()
        => new RequestHandlerWrapperImpl<CreateOrderCommand, Order>();
}
```

#### Generador 3: Optimized Mediator Dispatch
Genera implementación monomorfa de IMediator

```csharp
// Output generado:
public partial class GeneratedMediator : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        return request switch
        {
            GetProductQuery q => SendGetProductQuery(q, ct),
            CreateOrderCommand c => SendCreateOrderCommand(c, ct),
            _ => throw new InvalidOperationException($"No handler for {request.GetType()}")
        };
    }

    private async Task<Product> SendGetProductQuery(GetProductQuery request, CancellationToken ct)
    {
        // Pipeline behaviors inline
        // Direct handler invocation
    }
}
```

### 4.3 Flujo de Compilación

```
1. Usuario escribe handlers normalmente
   ↓
2. Roslyn analiza el código fuente
   ↓
3. Source Generator detecta tipos que implementan:
   - IRequestHandler<,>
   - INotificationHandler<>
   - IPipelineBehavior<,>
   - etc.
   ↓
4. Genera código de registro y dispatch
   ↓
5. Código generado se compila junto con el proyecto
   ↓
6. Resultado: Ejecutable AOT-compatible
```

---

## 5. Plan de Implementación

### Fase 1: Foundation (Semanas 1-4)

**Objetivo:** Crear la base del source generator

**Tareas:**
- [ ] Crear proyecto `MediateX.SourceGenerator` (netstandard2.0)
- [ ] Setup de Roslyn Compilation Pipeline
- [ ] Implementar `IIncrementalGenerator`
- [ ] Detectar tipos que implementan interfaces de MediateX
- [ ] Generar código básico de registro
- [ ] Tests unitarios del generador

**Entregable:** Generador que descubre handlers y genera `AddMediateXHandlers()`

**Archivos a crear:**
```
src/MediateX.SourceGenerator/
├── MediateXGenerator.cs           # Entry point del generador
├── HandlerDiscovery.cs            # Lógica de descubrimiento
├── CodeEmitter.cs                 # Generación de código
├── DiagnosticDescriptors.cs       # Warnings/errors de compilación
└── Extensions/
    └── SymbolExtensions.cs        # Helpers para Roslyn
```

### Fase 2: Core Migration (Semanas 5-8)

**Objetivo:** Reemplazar reflection en runtime

**Tareas:**
- [ ] Generador de wrappers pre-compilados
- [ ] Refactorizar `Mediator.cs` para usar wrappers generados
- [ ] Eliminar `Activator.CreateInstance`
- [ ] Generador de dispatch optimizado (switch-based)
- [ ] Mantener compatibilidad con API existente

**Entregable:** Mediator funcional sin reflection en runtime

**Archivos a modificar:**
```
src/MediateX/
├── Mediator.cs                    # Refactorizar para usar código generado
├── Mediator.Generated.cs          # Partial class generada
└── Registration/
    └── ServiceRegistrar.cs        # Simplificar o eliminar
```

### Fase 3: Advanced Features (Semanas 9-12)

**Objetivo:** Soportar características avanzadas

**Tareas:**
- [ ] Soporte para behaviors con generics anidados
- [ ] Soporte para notification handlers
- [ ] Soporte para stream handlers
- [ ] Soporte para exception handlers
- [ ] Optimizaciones de performance

**Entregable:** Feature parity con versión actual

### Fase 4: Polish (Semanas 13-14)

**Objetivo:** Preparar para release

**Tareas:**
- [ ] Documentación completa
- [ ] Migration guide
- [ ] Benchmarks comparativos
- [ ] Ejemplos actualizados
- [ ] Release notes

---

## 6. Cambios en la API

### 6.1 Cambios Transparentes (No breaking)

```csharp
// ANTES y DESPUÉS - Mismo código de usuario
public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    public Task<Product> Handle(GetProductQuery request, CancellationToken ct)
    {
        // ...
    }
}

// ANTES y DESPUÉS - Mismo registro
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

// ANTES y DESPUÉS - Mismo uso
var product = await mediator.Send(new GetProductQuery(42));
```

### 6.2 Cambios Potencialmente Breaking

#### Open Generic Behaviors

```csharp
// ANTES: Funcionaba con reflection
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// DESPUÉS: Requiere registro explícito o generación
// Opción A: El generador lo detecta y genera registros cerrados
// Opción B: Registro manual de cada combinación
```

**Decisión pendiente:** ¿Soportar open generics via generador o requerir registro explícito?

#### Assembly Scanning Dinámico

```csharp
// ANTES: Escaneo en runtime
cfg.RegisterServicesFromAssembly(Assembly.Load("MyPlugins"));

// DESPUÉS: Solo assemblies conocidos en compile-time
// Los plugins dinámicos NO son compatibles con AOT
```

**Decisión pendiente:** ¿Documentar limitación o proveer mecanismo alternativo?

---

## 7. Benchmarks Esperados

Basado en datos de librerías similares:

| Métrica | MediateX Actual | MediateX AOT (Estimado) | Mejora |
|---------|-----------------|-------------------------|--------|
| Startup time | ~100ms | ~1ms | **100x** |
| Memory at startup | ~20MB | ~2MB | **10x** |
| Request dispatch | ~1μs | ~0.1μs | **10x** |
| Cold start (Lambda) | ~3s | ~100ms | **30x** |

---

## 8. Riesgos y Mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| Complejidad de Roslyn | Media | Alto | Estudiar ejemplos existentes, incrementar gradualmente |
| Breaking changes | Media | Alto | Mantener modo "legacy" con reflection |
| Edge cases no cubiertos | Alta | Medio | Testing exhaustivo, beta period largo |
| Performance del generador | Baja | Bajo | Incremental generators son eficientes |

---

## 9. Recursos de Aprendizaje

### Source Generators
- [Introducing C# Source Generators](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/)
- [Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
- [Incremental Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)

### Ejemplos de Referencia
- [Mediator Source Code](https://github.com/martinothamar/Mediator/tree/main/src/Mediator.SourceGenerator)
- [System.Text.Json Source Generator](https://github.com/dotnet/runtime/tree/main/src/libraries/System.Text.Json/gen)
- [Jab - DI Container con Source Generators](https://github.com/pakrym/jab)

### Native AOT
- [Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [AOT Compatibility](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)

---

## 10. Decisiones Pendientes

- [ ] ¿Mantener modo reflection como fallback?
- [ ] ¿Soportar open generic behaviors automáticamente?
- [ ] ¿Qué versión de .NET mínima para el generador?
- [ ] ¿Separar en paquete NuGet independiente?
- [ ] ¿Nombre del paquete? `MediateX.SourceGenerator` vs `MediateX.Generators`

---

## 11. Conclusión

**Native AOT con Source Generators es técnicamente viable y traería beneficios significativos:**

1. **Performance:** Mejoras de 10-100x en startup y memoria
2. **Modernidad:** Alineación con el futuro de .NET
3. **Competitividad:** Feature parity con alternativas modernas
4. **Diferenciación:** MediateX sería el único fork de MediatR con AOT nativo

**Esfuerzo estimado:** 3-4 meses de desarrollo dedicado

**Próximo paso:** Crear spike/prototipo del generador básico para validar arquitectura.

---

*Documento generado: 2025-12-25*
*Próxima revisión: Pendiente de decisión de roadmap*
