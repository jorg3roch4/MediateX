# Investigacion: Source Generators en .NET
Fecha: 2026-01-09
Tarea: evolucion-mediatex v3.2/v4.0
Estado: completo

## Pregunta
Como funcionan los Source Generators y como aplicarlos a MediateX para eliminar reflection y soportar Native AOT?

---

## Resumen Ejecutivo

Los Source Generators son una caracteristica del compilador de C# que permite generar codigo fuente adicional durante la compilacion. Esto elimina la necesidad de reflection en runtime, mejorando drasticamente el rendimiento y habilitando Native AOT.

**Hallazgos clave:**
- Usar `IIncrementalGenerator` (no el viejo `ISourceGenerator`)
- El proyecto Mediator (martinothamar) es una excelente referencia de implementacion
- Se puede lograr ~40x mejor rendimiento vs reflection
- Requiere un paquete NuGet separado como analyzer

---

## 1. Fundamentos de Source Generators

### 1.1 Que son?

Los Source Generators son componentes que:
1. Se ejecutan **durante la compilacion** (no en runtime)
2. Analizan el codigo fuente existente usando las APIs de Roslyn
3. Generan archivos `.cs` adicionales que se incluyen en la compilacion
4. El codigo generado es visible y debuggeable

### 1.2 Ventajas sobre Reflection

| Aspecto | Reflection | Source Generator |
|---------|------------|------------------|
| Ejecucion | Runtime | Compile-time |
| Rendimiento | ~100x mas lento | Equivalente a codigo manual |
| Startup time | Lento (assembly scanning) | Instantaneo |
| Native AOT | No compatible | Totalmente compatible |
| Errores | Runtime exceptions | Compile-time errors |
| Memory | Allocations en cada request | Zero allocations |

### 1.3 Casos de uso ideales

- Serializacion (System.Text.Json)
- Dependency Injection (descubrimiento de servicios)
- Mediator pattern (dispatch de handlers)
- Logging (LoggerMessage)
- API clients (generacion desde OpenAPI)

---

## 2. IIncrementalGenerator vs ISourceGenerator

### 2.1 ISourceGenerator (OBSOLETO)

```csharp
// NO USAR - Deprecado desde .NET 6
[Generator]
public class OldGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }
    public void Execute(GeneratorExecutionContext context) { }
}
```

**Problemas:**
- Se ejecuta completamente en cada compilacion
- No tiene caching
- Lento en proyectos grandes
- Impacta negativamente la experiencia de desarrollo (IntelliSense lento)

### 2.2 IIncrementalGenerator (RECOMENDADO)

```csharp
[Generator]
public class ModernGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline de transformacion con caching automatico
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsCandidate(node),  // Filtro rapido
                transform: (ctx, _) => Transform(ctx))       // Transformacion
            .Where(x => x is not null);

        context.RegisterSourceOutput(provider, Generate);
    }
}
```

**Ventajas:**
- Caching automatico de resultados intermedios
- Solo re-ejecuta cuando cambia el codigo relevante
- Mucho mas rapido en compilaciones incrementales
- Mejor experiencia de desarrollo

---

## 3. Arquitectura del Pipeline

### 3.1 Flujo de datos

```
Compilation
     |
     v
SyntaxProvider.CreateSyntaxProvider()
     |
     |-- predicate: (SyntaxNode, CancellationToken) -> bool
     |       Filtro rapido en el syntax tree
     |
     |-- transform: (GeneratorSyntaxContext, CancellationToken) -> TModel
     |       Extraccion de semantic model
     |
     v
IncrementalValuesProvider<TModel>
     |
     |-- .Where(), .Select(), .Collect(), .Combine()
     |       Transformaciones adicionales
     |
     v
RegisterSourceOutput()
     |
     v
Codigo generado (.g.cs)
```

### 3.2 Puntos de caching

El pipeline cachea automaticamente despues de cada transformacion:

```csharp
context.SyntaxProvider
    .CreateSyntaxProvider(predicate, transform)  // Cache point 1
    .Where(x => x != null)                       // Cache point 2
    .Select(x => ProcessFurther(x))              // Cache point 3
    .Collect();                                  // Cache point 4
```

**Importante:** Las transformaciones deben retornar tipos **inmutables** y **equatable** para que el caching funcione.

---

## 4. Analisis del Source Generator de Mediator

### 4.1 Estructura del proyecto

```
Mediator.SourceGenerator/
├── IncrementalMediatorGenerator.cs     # Punto de entrada
├── Implementation/
│   ├── Analysis/
│   │   ├── CompilationAnalyzer.cs      # Descubre handlers y mensajes
│   │   ├── RequestMessage.cs           # Modelo de mensaje
│   │   ├── RequestMessageHandler.cs    # Modelo de handler
│   │   └── PipelineBehaviorType.cs     # Modelo de behavior
│   ├── Models/                          # Modelos inmutables para templates
│   │   ├── CompilationModel.cs
│   │   ├── RequestMessageModel.cs
│   │   └── RequestMessageHandlerModel.cs
│   ├── MediatorImplementationGenerator.cs  # Genera codigo
│   └── resources/
│       ├── Mediator.sbn-cs             # Template principal (Scriban)
│       └── MediatorOptions.sbn-cs
├── MediatorGeneratorStepName.cs
└── Mediator.SourceGenerator.csproj
```

### 4.2 Flujo de generacion

```csharp
// IncrementalMediatorGenerator.cs
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    // 1. Encontrar llamadas a AddMediator()
    var addMediatorCalls = context.SyntaxProvider.CreateSyntaxProvider(
        predicate: (s, _) => SyntaxReceiver.ShouldVisit(s, out _),
        transform: (ctx, _) => (InvocationExpressionSyntax)ctx.Node
    );

    // 2. Combinar con la compilacion
    var source = context.CompilationProvider.Combine(addMediatorCalls.Collect());

    // 3. Analizar y generar modelo
    var parsed = source.Select((x, token) => Parse(x.Compilation, x.AddMediatorCalls, token));

    // 4. Reportar errores
    context.RegisterSourceOutput(parsed.Select((x, _) => x.Diagnostics), ReportErrors);

    // 5. Generar codigo
    context.RegisterSourceOutput(parsed.Select((x, _) => x.Model), GenerateCode);
}
```

### 4.3 Como descubre handlers

```csharp
// CompilationAnalyzer.cs - Simplificado
private void PopulateMetadata(Queue<INamespaceOrTypeSymbol> queue)
{
    while (queue.Count > 0)
    {
        var nsOrTypeSymbol = queue.Dequeue();

        if (nsOrTypeSymbol is INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
                ProcessMember(queue, member);
        }
        else
        {
            ProcessMember(queue, (INamedTypeSymbol)nsOrTypeSymbol);
        }
    }
}

void ProcessMember(/* ... */)
{
    // Examina interfaces del tipo
    foreach (var iface in typeSymbol.AllInterfaces)
    {
        if (iface.Name == "IRequestHandler")
            // Registrar como handler
        else if (iface.Name == "IRequest")
            // Registrar como mensaje
    }
}
```

### 4.4 Codigo generado (simplificado)

```csharp
// Mediator.g.cs generado
namespace Microsoft.Extensions.DependencyInjection
{
    public static class MediatorDependencyInjectionExtensions
    {
        public static IServiceCollection AddMediator(this IServiceCollection services)
        {
            // Registros directos sin reflection
            services.Add(new ServiceDescriptor(
                typeof(IRequestHandler<GetProductQuery, Product>),
                typeof(GetProductHandler),
                ServiceLifetime.Transient));

            // Wrapper pre-generado
            services.Add(new ServiceDescriptor(
                typeof(RequestHandlerWrapper_GetProductQuery_Product),
                typeof(RequestHandlerWrapper_GetProductQuery_Product),
                ServiceLifetime.Singleton));

            return services;
        }
    }
}

namespace MyApp
{
    public sealed class Mediator : IMediator
    {
        private readonly ContainerMetadata _metadata;

        // Metodo tipado para cada mensaje - SIN REFLECTION
        public ValueTask<Product> Send(GetProductQuery request, CancellationToken ct)
        {
            var wrapper = _metadata.GetProductQueryHandler;
            return wrapper.Handle(request, ct);
        }

        // Para dispatch dinamico usa FrozenDictionary
        public ValueTask<object?> Send(object request, CancellationToken ct)
        {
            var wrapper = _metadata.RequestHandlerWrappers[request.GetType()];
            return wrapper.Handle(this, request, ct);
        }
    }
}
```

### 4.5 Optimizaciones clave

1. **FrozenDictionary**: Diccionario inmutable optimizado para lectura
   ```csharp
   // Se construye una sola vez al inicio
   RequestHandlerWrappers = FrozenDictionary.ToFrozenDictionary(dict);
   ```

2. **Metodos tipados generados**: Un metodo `Send()` por cada tipo de mensaje
   ```csharp
   // Generado para cada IRequest
   public ValueTask<Product> Send(GetProductQuery request, CancellationToken ct)
   ```

3. **Wrappers pre-instanciados**: No hay creacion de objetos en cada request
   ```csharp
   // Singleton wrapper, inicializado una vez
   public readonly RequestHandlerWrapper_GetProductQuery_Product GetProductQueryHandler;
   ```

4. **Pipeline pre-compilado**: Behaviors encadenados sin LINQ
   ```csharp
   // En lugar de .Aggregate(), genera codigo directo
   return _loggingBehavior.Handle(request,
       ct => _validationBehavior.Handle(request,
           ct => handler.Handle(request, ct), ct), ct);
   ```

---

## 5. Configuracion del Proyecto Source Generator

### 5.1 Archivo .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- DEBE ser netstandard2.0 para maxima compatibilidad -->
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>12.0</LangVersion>

    <!-- Configuracion de paquete analyzer -->
    <IsPackable>true</IsPackable>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>

    <!-- Habilitar depuracion -->
    <IsRoslynComponent>true</IsRoslynComponent>
  </PropertyGroup>

  <ItemGroup>
    <!-- El DLL se incluye como analyzer, no como dependencia -->
    <None Include="$(OutputPath)\$(AssemblyName).dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>

  <ItemGroup>
    <!-- Dependencias de Roslyn -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.1.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.3" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### 5.2 Dependencias requeridas

| Paquete | Proposito | Nota |
|---------|-----------|------|
| `Microsoft.CodeAnalysis.CSharp` | APIs de Roslyn | Version 4.1.0+ |
| `Microsoft.CodeAnalysis.Analyzers` | Analisis de codigo | PrivateAssets="all" |
| `Scriban` (opcional) | Templates de codigo | Solo si usas templating |
| `PolySharp` (opcional) | Polyfills modernos | Para usar features de C# 12+ |

---

## 6. Mejores Practicas

### 6.1 Diseno del Generator

| Practica | Descripcion |
|----------|-------------|
| Filtrar temprano | El `predicate` debe ser muy rapido y rechazar la mayoria de nodos |
| Modelos inmutables | Usar `record` o `readonly struct` para los modelos |
| Implementar `IEquatable<T>` | Esencial para que el caching funcione |
| Evitar closures | Usar metodos `static` en lambdas |
| Reportar diagnosticos | Usar `context.ReportDiagnostic()` para errores |

### 6.2 Codigo generado

| Practica | Descripcion |
|----------|-------------|
| Marcar con atributos | `[GeneratedCode("Name", "Version")]` |
| Usar `#nullable enable` | Consistencia con codigo moderno |
| Suprimir warnings | `#pragma warning disable CS8019` |
| Nombres unicos | Evitar colisiones con codigo del usuario |
| Partial classes | Permitir extension por el usuario |

### 6.3 Testing

```csharp
// Usar Verify para snapshot testing
[Fact]
public Task GeneratesCorrectCode()
{
    var source = @"
        public record GetProductQuery(int Id) : IRequest<Product>;
        public class GetProductHandler : IRequestHandler<GetProductQuery, Product> { }
    ";

    return Verify(source);
}
```

### 6.4 Depuracion

1. Agregar `<IsRoslynComponent>true</IsRoslynComponent>` al .csproj
2. En VS: Debug > Attach to Process > dotnet.exe (compilador)
3. O usar `System.Diagnostics.Debugger.Launch()` en el generator

---

## 7. Plan de Implementacion para MediateX

### 7.1 Fase 1: Spike (v3.2)

**Objetivo:** Validar la arquitectura con un prototipo minimo

```
MediateX.SourceGenerator/
├── MediateXGenerator.cs              # IIncrementalGenerator
├── Analysis/
│   ├── MediateXAnalyzer.cs           # Descubre handlers
│   └── Models.cs                     # Modelos inmutables
├── Generation/
│   └── MediateXCodeGenerator.cs      # Genera codigo
└── MediateX.SourceGenerator.csproj
```

**Scope minimo:**
- Descubrir `IRequestHandler<TRequest, TResponse>`
- Generar registro directo en DI
- Generar metodos `Send<TResponse>(IRequest<TResponse>)`
- Sin behaviors inicialmente

### 7.2 Fase 2: Handlers completos (v3.3)

- Soporte para `INotificationHandler`
- Soporte para `IStreamRequestHandler`
- Behaviors pipeline generado

### 7.3 Fase 3: Paquete publico (v4.0)

- MediateX.SourceGenerator como paquete NuGet separado
- Documentacion completa
- Diagnosticos de compilacion
- Native AOT verificado

### 7.4 Arquitectura dual-mode

```csharp
// El usuario puede elegir:

// Modo 1: Reflection (compatible, default)
services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

// Modo 2: Source Generator (opt-in, maximo rendimiento)
services.AddMediateXGenerated();  // Generado por SG
```

---

## 8. Riesgos y Mitigaciones

| Riesgo | Probabilidad | Mitigacion |
|--------|--------------|------------|
| Complejidad de Roslyn APIs | Media | Estudiar implementacion de Mediator |
| Debugging dificil | Media | Usar `IsRoslynComponent`, snapshot tests |
| Errores en edge cases | Alta | Tests exhaustivos, diagnosticos claros |
| Compatibilidad con DI containers | Baja | Usar solo MS.Extensions.DI |
| Performance del compilador | Baja | Pipeline incremental, filtros rapidos |

---

## 9. Fuentes

### Documentacion oficial
- [Source Generators Overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [Incremental Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)

### Articulos
- [.NET Handbook - Source Generators](https://infinum.com/handbook/dotnet/best-practices/source-generators)
- [Deep dive into Source Generators](https://thecodeman.net/posts/source-generators-deep-dive)
- [Mastering Incremental Source Generators](https://blog.elmah.io/mastering-incremental-source-generators-in-csharp-a-complete-guide-with-example/)

### Implementaciones de referencia
- [Mediator (martinothamar)](https://github.com/martinothamar/Mediator) - Referencia principal
- [System.Text.Json](https://github.com/dotnet/runtime/tree/main/src/libraries/System.Text.Json) - Serializacion
- [LoggerMessage](https://docs.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) - Logging

### Codigo analizado
- `references/Mediator/src/Mediator.SourceGenerator/` - Implementacion completa

---

## 10. Conclusiones

1. **Source Generators son el camino a seguir** para MediateX v4.0
2. **IIncrementalGenerator** es obligatorio (no usar el viejo ISourceGenerator)
3. **El proyecto Mediator** es una excelente referencia de implementacion
4. **El spike debe empezar simple**: solo `IRequestHandler`, luego expandir
5. **Mantener modo reflection** como fallback para plugins dinamicos
6. **FrozenDictionary** debe usarse tambien en el modo reflection (v3.2)

**Siguiente paso:** Crear el spike del Source Generator en `src/MediateX.SourceGenerator/`

---

*Documento creado: 2026-01-09*
*Basado en: analisis de Mediator (martinothamar), documentacion oficial, articulos de la comunidad*
