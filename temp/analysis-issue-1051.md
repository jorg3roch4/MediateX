# Análisis Técnico Profundo: Issue #1051 - AddOpenBehavior con Genéricos Anidados

## 1. Definición del Problema

### Escenario que falla
```csharp
// Behavior con genérico anidado en el response
public class ResultBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : IRequest<Result<TValue>>
{
    public async Task<Result<TValue>> Handle(TRequest request,
        RequestHandlerDelegate<Result<TValue>> next, CancellationToken ct)
    {
        Console.WriteLine("Before"); // NUNCA SE EJECUTA
        var result = await next();
        Console.WriteLine("After");  // NUNCA SE EJECUTA
        return result;
    }
}

// Request concreto
public class GetStringQuery : IRequest<Result<string>> { }

// Registro
services.AddMediateX(cfg => {
    cfg.RegisterServicesFromAssemblyContaining<GetStringQuery>();
    cfg.AddOpenBehavior(typeof(ResultBehavior<,>)); // ❌ No funciona
});
```

### Por qué falla

El código actual en `AddOpenBehavior`:
```csharp
public MediateXServiceConfiguration AddOpenBehavior(Type openBehaviorType, ...)
{
    // Registra: IPipelineBehavior<,> → ResultBehavior<,>
    BehaviorsToRegister.Add(new(typeof(IPipelineBehavior<,>), openBehaviorType, serviceLifetime));
}
```

Al resolver `IPipelineBehavior<GetStringQuery, Result<string>>`:
1. DI busca registros de `IPipelineBehavior<,>`
2. Encuentra `ResultBehavior<,>` que implementa `IPipelineBehavior<TRequest, Result<TValue>>`
3. **FALLA**: DI no puede inferir `TValue = string` desde `Result<string> = Result<TValue>`

## 2. Solución: Algoritmo de Unificación de Tipos

### 2.1 Fundamento Teórico

La unificación de tipos es un algoritmo estándar en teoría de tipos usado por compiladores para inferencia de tipos. Dado un patrón y un tipo concreto, encuentra las sustituciones de variables de tipo que hacen que ambos sean iguales.

### 2.2 Algoritmo Propuesto

```
FUNCTION UnifyTypes(pattern, concrete, bindings) → bool

INPUT:
  - pattern: Tipo con posibles parámetros genéricos (ej: Result<TValue>)
  - concrete: Tipo concreto (ej: Result<string>)
  - bindings: Diccionario mutable de sustituciones (ej: {TValue → string})

OUTPUT:
  - true si unificación exitosa, false si imposible

ALGORITHM:
  IF pattern es parámetro genérico (IsGenericParameter):
    IF bindings contiene pattern:
      RETURN bindings[pattern] == concrete
    ELSE:
      bindings[pattern] = concrete
      RETURN true

  IF pattern es tipo genérico construido (IsGenericType && !IsGenericTypeDefinition):
    IF concrete NO es genérico:
      RETURN false
    IF pattern.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition():
      RETURN false

    patternArgs = pattern.GetGenericArguments()
    concreteArgs = concrete.GetGenericArguments()

    FOR i = 0 TO patternArgs.Length - 1:
      IF NOT UnifyTypes(patternArgs[i], concreteArgs[i], bindings):
        RETURN false

    RETURN true

  // Tipos no genéricos deben coincidir exactamente
  RETURN pattern == concrete
```

### 2.3 Ejemplo de Ejecución

**Entrada:**
- Behavior: `ResultBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>`
- Request: `GetStringQuery : IRequest<Result<string>>`

**Paso 1: Extraer información**
```
behaviorInterface = IPipelineBehavior<TRequest, Result<TValue>>
behaviorGenericParams = [TRequest, TValue]
requestType = GetStringQuery
responseType = Result<string>  // desde IRequest<Result<string>>
```

**Paso 2: Unificar TRequest**
```
UnifyTypes(TRequest, GetStringQuery, {})
→ TRequest es GenericParameter
→ bindings[TRequest] = GetStringQuery
→ RETURN true
→ bindings = {TRequest: GetStringQuery}
```

**Paso 3: Unificar Response**
```
UnifyTypes(Result<TValue>, Result<string>, {TRequest: GetStringQuery})
→ Result<TValue> es GenericType
→ Result<string> es GenericType
→ GetGenericTypeDefinition: Result<> == Result<> ✓
→ Recursión: UnifyTypes(TValue, string, bindings)
  → TValue es GenericParameter
  → bindings[TValue] = string
  → RETURN true
→ RETURN true
→ bindings = {TRequest: GetStringQuery, TValue: string}
```

**Paso 4: Cerrar el tipo**
```
closedBehavior = ResultBehavior<,>.MakeGenericType(GetStringQuery, string)
               = ResultBehavior<GetStringQuery, string>

closedInterface = IPipelineBehavior<GetStringQuery, Result<string>>
```

**Paso 5: Registrar**
```
services.AddTransient(
    IPipelineBehavior<GetStringQuery, Result<string>>,
    ResultBehavior<GetStringQuery, string>
)
```

## 3. Casos Edge y Cómo Manejarlos

### 3.1 Múltiples Niveles de Anidación

```csharp
// Behavior
class DeepBehavior<TReq, T1, T2> : IPipelineBehavior<TReq, Result<Dict<T1, List<T2>>>>
    where TReq : IRequest<Result<Dict<T1, List<T2>>>>

// Request
class MyQuery : IRequest<Result<Dict<int, List<string>>>>
```

**Unificación:**
```
UnifyTypes(Result<Dict<T1, List<T2>>>, Result<Dict<int, List<string>>>, {})
→ UnifyTypes(Dict<T1, List<T2>>, Dict<int, List<string>>, {})
  → UnifyTypes(T1, int, {}) → bindings[T1] = int
  → UnifyTypes(List<T2>, List<string>, {T1: int})
    → UnifyTypes(T2, string, {T1: int}) → bindings[T2] = string
→ bindings = {T1: int, T2: string}
```

**Resultado:** ✅ Soportado por el algoritmo recursivo

### 3.2 Constraints Complejos

```csharp
class ValidatingBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : IRequest<TRes>, IValidatable
    where TRes : class, new()
```

**Manejo:**
1. Después de la unificación, verificar constraints con `MakeGenericType`
2. Si falla, el tipo no es compatible - capturar excepción y continuar

```csharp
try
{
    var closed = openBehavior.MakeGenericType(typeArgs);
    // Registrar
}
catch (ArgumentException)
{
    // Constraints no satisfechos, saltar este request
}
```

### 3.3 Parámetros No Utilizados en Response

```csharp
// TExtra no aparece en el response type
class LoggingBehavior<TReq, TRes, TExtra> : IPipelineBehavior<TReq, TRes>
    where TExtra : ILogger
```

**Problema:** No se puede inferir TExtra desde el request/response

**Manejo:**
- Detectar parámetros sin binding después de unificación
- Buscar tipos que satisfagan los constraints
- Generar combinaciones (similar a handlers genéricos actuales)

### 3.4 Mismo Parámetro en Múltiples Posiciones

```csharp
class SameTypeBehavior<TReq, T> : IPipelineBehavior<TReq, Pair<T, T>>
    where TReq : IRequest<Pair<T, T>>

// Request con tipos diferentes - NO debe coincidir
class MismatchQuery : IRequest<Pair<int, string>>
```

**Unificación:**
```
UnifyTypes(Pair<T, T>, Pair<int, string>, {})
→ UnifyTypes(T, int, {}) → bindings[T] = int
→ UnifyTypes(T, string, {T: int})
  → T ya existe en bindings
  → int != string
  → RETURN false ❌
```

**Resultado:** ✅ Correctamente rechazado por el algoritmo

### 3.5 Covarianza/Contravarianza

```csharp
interface IProducer<out T> { }
interface IConsumer<in T> { }

class VariantBehavior<TReq, T> : IPipelineBehavior<TReq, IProducer<T>>
```

**Manejo:** Para registro, requerimos coincidencia exacta de tipos, no asignabilidad. La varianza solo afecta uso en runtime, no registro.

## 4. Implementación Propuesta

### 4.1 Nueva Clase: TypeUnifier

```csharp
internal static class TypeUnifier
{
    /// <summary>
    /// Attempts to unify a type pattern with a concrete type, extracting type parameter bindings.
    /// </summary>
    public static bool TryUnify(Type pattern, Type concrete, Dictionary<Type, Type> bindings)
    {
        // Caso 1: pattern es un parámetro genérico
        if (pattern.IsGenericParameter)
        {
            if (bindings.TryGetValue(pattern, out var existing))
            {
                return existing == concrete;
            }
            bindings[pattern] = concrete;
            return true;
        }

        // Caso 2: pattern es un tipo genérico construido
        if (pattern.IsGenericType && !pattern.IsGenericTypeDefinition)
        {
            if (!concrete.IsGenericType)
                return false;

            if (pattern.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition())
                return false;

            var patternArgs = pattern.GetGenericArguments();
            var concreteArgs = concrete.GetGenericArguments();

            if (patternArgs.Length != concreteArgs.Length)
                return false;

            for (int i = 0; i < patternArgs.Length; i++)
            {
                if (!TryUnify(patternArgs[i], concreteArgs[i], bindings))
                    return false;
            }

            return true;
        }

        // Caso 3: tipos no genéricos deben coincidir exactamente
        return pattern == concrete;
    }

    /// <summary>
    /// Checks if a behavior type has nested generics in its response type.
    /// </summary>
    public static bool HasNestedGenericsInResponseType(Type behaviorType)
    {
        var pipelineInterface = behaviorType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (pipelineInterface == null)
            return false;

        var responseType = pipelineInterface.GetGenericArguments()[1];

        // Si el response type es un genérico construido que contiene parámetros genéricos
        return responseType.IsGenericType &&
               !responseType.IsGenericParameter &&
               responseType.GetGenericArguments().Any(arg =>
                   arg.IsGenericParameter ||
                   (arg.IsGenericType && ContainsGenericParameters(arg)));
    }

    private static bool ContainsGenericParameters(Type type)
    {
        if (type.IsGenericParameter) return true;
        if (type.IsGenericType)
        {
            return type.GetGenericArguments().Any(ContainsGenericParameters);
        }
        return false;
    }
}
```

### 4.2 Modificación a AddOpenBehavior

```csharp
public MediateXServiceConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
{
    if (!openBehaviorType.IsGenericType)
        throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");

    // Verificar que implementa IPipelineBehavior<,>
    var pipelineInterface = openBehaviorType.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

    if (pipelineInterface == null)
        throw new InvalidOperationException($"{openBehaviorType.Name} must implement IPipelineBehavior<,>");

    // Si tiene genéricos anidados, necesitamos cerrar manualmente
    if (TypeUnifier.HasNestedGenericsInResponseType(openBehaviorType))
    {
        // Marcar para procesamiento posterior cuando tengamos los assemblies
        _pendingNestedGenericBehaviors.Add((openBehaviorType, serviceLifetime));
    }
    else
    {
        // Comportamiento actual para behaviors simples
        BehaviorsToRegister.Add(new(typeof(IPipelineBehavior<,>), openBehaviorType, serviceLifetime));
    }

    return this;
}

// Llamado durante AddMediateXClasses después de escanear assemblies
internal void ProcessPendingNestedGenericBehaviors(IEnumerable<Assembly> assemblies)
{
    var requestTypes = assemblies
        .SelectMany(a => a.GetTypes())
        .Where(t => t.IsClass && !t.IsAbstract)
        .Where(t => t.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
        .ToList();

    foreach (var (behaviorType, lifetime) in _pendingNestedGenericBehaviors)
    {
        foreach (var requestType in requestTypes)
        {
            if (TryCreateClosedBehaviorRegistration(behaviorType, requestType, out var registration))
            {
                BehaviorsToRegister.Add(new(registration.ServiceType, registration.ImplementationType, lifetime));
            }
        }
    }
}

private bool TryCreateClosedBehaviorRegistration(Type openBehavior, Type requestType,
    out (Type ServiceType, Type ImplementationType) registration)
{
    registration = default;

    // Obtener el response type del request
    var requestInterface = requestType.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

    if (requestInterface == null) return false;

    var responseType = requestInterface.GetGenericArguments()[0];

    // Obtener la interface del behavior
    var behaviorInterface = openBehavior.GetInterfaces()
        .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

    var patternRequest = behaviorInterface.GetGenericArguments()[0];
    var patternResponse = behaviorInterface.GetGenericArguments()[1];

    // Intentar unificar
    var bindings = new Dictionary<Type, Type>();

    if (!TypeUnifier.TryUnify(patternRequest, requestType, bindings))
        return false;

    if (!TypeUnifier.TryUnify(patternResponse, responseType, bindings))
        return false;

    // Verificar que todos los parámetros genéricos tienen binding
    var genericParams = openBehavior.GetGenericArguments();
    var typeArgs = new Type[genericParams.Length];

    for (int i = 0; i < genericParams.Length; i++)
    {
        if (!bindings.TryGetValue(genericParams[i], out var boundType))
            return false; // Parámetro sin binding
        typeArgs[i] = boundType;
    }

    // Intentar cerrar el tipo (verifica constraints)
    try
    {
        var closedBehavior = openBehavior.MakeGenericType(typeArgs);
        var closedInterface = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);

        registration = (closedInterface, closedBehavior);
        return true;
    }
    catch (ArgumentException)
    {
        // Constraints no satisfechos
        return false;
    }
}
```

## 5. Análisis de Riesgos

### 5.1 Riesgos Técnicos

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| Rendimiento en startup con muchos tipos | Media | Bajo | Cachear resultados, lazy evaluation |
| Regresión en behaviors existentes | Baja | Alto | Tests exhaustivos, flag de opt-in |
| Casos edge no contemplados | Media | Medio | Tests comprehensivos, logging |
| Incompatibilidad con DI containers externos | Baja | Medio | Solo afecta registro, no resolución |

### 5.2 Complejidad de Mantenimiento

- **Código nuevo:** ~200-300 líneas
- **Complejidad algorítmica:** O(requests × behaviors × type_depth)
- **Dependencias:** Ninguna nueva
- **Tests requeridos:** ~15-20 casos

### 5.3 Breaking Changes

**Ninguno** - El comportamiento actual se mantiene:
- Behaviors simples siguen funcionando igual
- Solo behaviors con genéricos anidados se procesan diferente
- Si la unificación falla, simplemente no se registra (igual que antes)

## 6. Plan de Testing

### 6.1 Tests Unitarios para TypeUnifier

```csharp
[Theory]
[InlineData(typeof(string), typeof(string), true)]  // Exacto
[InlineData(typeof(int), typeof(string), false)]    // Diferentes
public void TryUnify_NonGenericTypes(Type pattern, Type concrete, bool expected)

[Fact]
public void TryUnify_SingleGenericParameter_BindsCorrectly()

[Fact]
public void TryUnify_NestedGenerics_BindsAllParameters()

[Fact]
public void TryUnify_SameParameterTwice_MustMatch()

[Fact]
public void TryUnify_DeeplyNested_WorksRecursively()
```

### 6.2 Tests de Integración

```csharp
[Fact]
public async Task Behavior_WithNestedGeneric_IsInvoked()

[Fact]
public async Task Behavior_WithMultipleNestedGenerics_IsInvoked()

[Fact]
public async Task Behavior_WithConstraints_OnlyMatchesValidRequests()

[Fact]
public async Task MultipleBehaviors_WithNestedGenerics_AllInvoked()
```

### 6.3 Tests de Regresión

```csharp
[Fact]
public async Task SimpleBehavior_StillWorks()

[Fact]
public async Task ExistingPipelineTests_StillPass()
```

## 7. Conclusiones

### Factibilidad: ✅ ALTA

El algoritmo de unificación de tipos es bien entendido y probado en compiladores. La implementación es directa.

### Complejidad: MEDIA

- Algoritmo recursivo pero con casos base claros
- Edge cases manejables
- No requiere cambios arquitecturales

### Riesgo: BAJO

- Sin breaking changes
- Fácil de testear
- Fallback seguro (no registrar si falla)

### Recomendación

**Proceder con la implementación** con las siguientes consideraciones:
1. Implementar TypeUnifier como clase interna
2. Agregar tests exhaustivos antes de modificar AddOpenBehavior
3. Considerar flag de configuración para habilitar/deshabilitar
4. Documentar la nueva capacidad
