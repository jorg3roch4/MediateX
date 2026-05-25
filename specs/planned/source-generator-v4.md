# [EVOLUCIÓN] Evolucionar MediateX
Iniciado: 2025-12-29
Estado: en-progreso

## Objetivo
Evolucionar MediateX hacia la versión 4.0 con tres ejes principales:
1. **Nuevas características** - Expandir funcionalidad con features nuevos
2. **Mejoras de rendimiento** - Optimizar performance, reducir allocations
3. **Preparación v4.0** - Roadmap con breaking changes planificados y nueva arquitectura

## Investigación
| Tema | Estado | Archivo |
|------|--------|---------|
| Evolución MediateX v4.0 | en-progreso | `research/2025-12-29_evolucion-mediatex-v4.md` |
| Source Generators | completo | `research/2026-01-09_source-generators-investigation.md` |

### Documentos Base (temp/)
- `temp/AOT-SourceGenerators-Investigation.md` - Investigación técnica AOT
- `temp/EVOLUTION-ROADMAP.md` - Roadmap original de evolución

### Notas Rápidas
- Versión actual: **3.3.0** ✅
- Target: .NET 10 (C# 14)
- Próximo target planificado: .NET 10 + 11 (v4.x)
- **MediateX es ahora un mediator puro** (sin cross-cutting concerns)
- **2,713 LOC** (-39% desde v3.1.x)
- **168 tests** (core functionality)
- MediatR ahora es comercial - oportunidad de diferenciación
- **Spike Source Generator completado** ✅

## Decisiones
| Fecha | Decisión | Justificación |
|-------|----------|---------------|
| 2025-12-29 | Arquitectura dual-mode (Reflection + SG) | Compatibilidad hacia atrás + máximo rendimiento |
| 2025-12-29 | Source generator como paquete separado | Opt-in explícito, no afecta usuarios existentes |
| 2025-12-29 | Mantener reflection como fallback en SG mode | Soportar plugins y assemblies dinámicos |

## Progreso
- [x] Investigar estado actual del código y áreas de mejora
- [x] Definir roadmap de nuevas características
- [x] Identificar oportunidades de optimización de rendimiento
- [x] Planificar breaking changes para v4.0
- [x] Documentar arquitectura propuesta
- [x] **v3.1.0** (2025-12-29) - Features adicionales
- [x] **v3.1.1** (2025-01-08) - Namespace fix
- [x] **v3.2.0 COMPLETADO** (2026-01-09):
  - [x] Eliminar Result<T> pattern (usar FluentResults, ErrorOr)
  - [x] Eliminar ValidationBehavior (usar FluentValidation)
  - [x] Eliminar LoggingBehavior (usuario implementa su propio)
  - [x] Eliminar RetryBehavior (usar Polly)
  - [x] Eliminar TimeoutBehavior (usar Polly)
  - [x] Eliminar dependencia Microsoft.Extensions.Logging.Abstractions
  - [x] MediateX es ahora un mediator puro (-39% código)
  - [x] **Spike Source Generator completado**
    - [x] Proyecto MediateX.SourceGenerator creado
    - [x] IIncrementalGenerator implementado
    - [x] Detecta IRequestHandler, INotificationHandler
    - [x] Genera AddMediateXGenerated() sin reflection
    - [x] Validada coexistencia con AddMediateX()
- [x] **v3.3.0 COMPLETADO** (2026-01-19):
  - [x] Implementar FrozenDictionary cache
  - [x] Método Mediator.Freeze() público
  - [x] Propiedad Mediator.IsFrozen
  - [x] 11 tests para funcionalidad Freeze
  - [x] Benchmarks comparativos (~7% mejora)
- [ ] Implementar ValueTask handlers (v4.0)
- [ ] Source Generator completo (v4.0)

## Arquitectura v4.0 Propuesta
```
MediateX v4.0
├── MediateX (Core)           ← Reflection mode (default, compatible)
└── MediateX.SourceGenerator  ← Compile-time (opt-in, AOT, ~40x faster)
    └── IMediator API unificada
```

## Plan de Releases
| Versión | Features | Estado |
|---------|----------|--------|
| v3.1.0 | Result<T>, ValidationBehavior, LoggingBehavior, RetryBehavior, TimeoutBehavior | ✅ Completado |
| v3.1.1 | Result types en MediateX.Contracts namespace | ✅ Completado |
| v3.2.0 | **Mediator puro** - eliminados cross-cutting concerns, spike SG | ✅ **Completado** |
| v3.3.0 | FrozenDictionary cache, Mediator.Freeze() | ✅ **Completado** |
| v4.0.0 | Source Generator completo, Native AOT | Pendiente |

## Registro de Sesiones
### 2025-12-29
- Tarea iniciada
- Objetivos definidos: nuevas características, mejoras de rendimiento, preparación v4.0
- Consolidada investigación de `temp/AOT-SourceGenerators-Investigation.md` y `temp/EVOLUTION-ROADMAP.md`
- Creado documento de investigación profunda: `research/2025-12-29_evolucion-mediatex-v4.md`
- Análisis del código actual: 2,715 LOC, 76 usos de reflection
- Investigado .NET 10 LTS: NativeAOT <5MB, dynamic AOT assemblies, C# 14 features
- Definida arquitectura dual-mode (Reflection + Source Generator)
- Establecido plan de releases v3.1 → v4.1
- **v3.1.0 COMPLETADO** con 92 nuevos tests (263 total):
  - `Result<T>` pattern: Result<T>, Result, Error, Match, Map, Bind (42 tests)
  - `ValidationBehavior`: IRequestValidator, ValidationResult, ValidationException (21 tests)
  - `LoggingBehavior`: LoggerMessage source gen, slow request detection (8 tests)
  - `RetryBehavior`: exponential backoff, jitter, ShouldRetryException (11 tests)
  - `TimeoutBehavior`: per-request timeout, IHasTimeout interface (10 tests)
  - Fluent Configuration API: AddValidationBehavior, AddLoggingBehavior, AddRetryBehavior, AddTimeoutBehavior
  - Variantes `*ResultBehavior` para integración con Result<T>

### 2025-01-08
- **v3.1.1 COMPLETADO** - Namespace conflict fix:
  - Problema: Result<T> de MediateX conflictuaba con Result<T> definidos por usuarios
  - Solución: Mover Result types a `MediateX.Contracts` namespace (opt-in explícito)
  - Archivos modificados: Result.cs, IResultRequest.cs, behaviors, tests, samples
  - Documentación actualizada: README.md, docs/04-behaviors.md, docs/05-configuration.md
  - Test de regresión: UserDefinedResultTests.cs (4 tests)
  - Publicado en NuGet: MediateX 3.1.1

### 2026-01-09
- **v3.2.0 COMPLETADO** - MediateX puro + Spike Source Generator:
  - **Decisión**: MediateX debe ser un mediator puro, cross-cutting concerns no pertenecen aquí
  - **Eliminado**: Result<T>, ValidationBehavior, LoggingBehavior, RetryBehavior, TimeoutBehavior
  - **Eliminado**: Dependencia Microsoft.Extensions.Logging.Abstractions
  - **Resultado**: -39% código (4,438 → 2,713 LOC), 168 tests (core functionality)
  - **Investigación Source Generators**: Documento completo en `research/2026-01-09_source-generators-investigation.md`
  - **Spike Source Generator completado**:
    - Creado `src/MediateX.SourceGenerator/` con IIncrementalGenerator
    - Detecta IRequestHandler, INotificationHandler en compile-time
    - Genera `AddMediateXGenerated()` sin reflection
    - Creado sample `samples/MediateX.Examples.SourceGenerator/`
    - Validado: handlers se ejecutan correctamente, coexiste con AddMediateX()
  - **Documentación actualizada (final)**:
    - README.md: Completamente reescrito - "Simple. Focused. No bloat.", ejemplos claros, tablas "What's Included" vs "What's NOT Included"
    - CHANGELOG.md: Sección v3.2.0 mejorada con filosofía "A Pure Mediator", tablas de impacto
    - docs/01-getting-started.md: Intro actualizada con enfoque pure mediator
    - docs/04-behaviors.md: Eliminada sección "Built-in Behaviors", añadida "Recommended Libraries"
    - docs/05-configuration.md: Eliminada configuración de behaviors built-in
  - **Spike Source Generator**: Conservado como referencia para v4.0
  - **Próximo**: v4.0 con Source Generator completo

### 2026-01-19
- **v3.3.0 COMPLETADO** - FrozenDictionary cache:
  - Implementado patrón dual cache (warmup ConcurrentDictionary + frozen FrozenDictionary)
  - Nuevo método `Mediator.Freeze()` para congelar caches después del warmup
  - Nueva propiedad `Mediator.IsFrozen` para verificar estado
  - Método interno `Mediator.Reset()` para tests
  - Agregado `InternalsVisibleTo` para MediateX.Tests y MediateX.Benchmarks
  - 11 nuevos tests en `FreezeTests.cs`
  - Benchmarks agregados en `FrozenBenchmarks` class
  - Resultados: ~7% mejora en Send operations después de Freeze()
  - Sin breaking changes - código existente funciona sin modificaciones
  - Actualizado CHANGELOG.md, versión a 3.3.0
