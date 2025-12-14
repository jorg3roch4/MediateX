# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [3.0.0] - 2025-12-13

This release brings MediateX to .NET 10, with significant improvements to DI container compatibility, assembly scanning robustness, and project structure alignment with .NET ecosystem conventions.

### ⚠️ Breaking Changes

*   **Target Framework:** Upgraded from .NET 9 to .NET 10 with C# 14
    *   All projects now target `net10.0` with `LangVersion 14`
    *   Microsoft.Extensions.* packages updated from 9.0.0 to 10.0.0
*   **Project Structure:** Reorganized source code to `src/MediateX/` following .NET ecosystem conventions
    *   Source code moved from `src/` to `src/MediateX/`
    *   All project references updated accordingly

### ✨ Added

*   **Assembly Scanning Robustness (MediatR #1140):** Added resilient type loading to handle assemblies with unloadable types
    *   New `GetLoadableDefinedTypes()` helper method catches `ReflectionTypeLoadException`
    *   New `GetLoadableTypes()` helper method for safe type enumeration
    *   Prevents crashes when scanning assemblies containing F# `inref`/`outref` types or other ByRef-like types
    *   Applied to all 3 vulnerable locations in `ServiceRegistrar.cs`

### 🔄 Changed

*   **Mediator Constructor Simplification:** Changed from 2 constructors to 1 with optional parameter
    *   Before: Two constructors (with and without `INotificationPublisher`)
    *   After: Single constructor with `INotificationPublisher? publisher = null`
    *   Improves compatibility with DI containers that require single public constructors (SimpleInjector, LightInject, etc.)
*   **Example Projects Modernization:**
    *   **Windsor:** Removed legacy `ServiceFactory` pattern, created `WindsorServiceProvider` adapter using `IServiceProvider`
    *   **SimpleInjector:** Added explicit `INotificationPublisher` registration for new constructor pattern
    *   **LightInject:** Complete rewrite with `LightInjectServiceProvider` adapter implementing `IServiceProvider`
*   **Dependency Cleanup:**
    *   Windsor example: Removed unnecessary `Newtonsoft.Json` and `Castle.Windsor.Extensions.DependencyInjection` dependencies
    *   LightInject example: Removed `LightInject.Microsoft.DependencyInjection` in favor of native adapter

### 📚 Documentation

*   **Primary Constructor Warning (MediatR #1119):** Added documentation about using records instead of primary constructors for request handlers
    *   New warning section in `docs/02-requests-handlers.md`
    *   Explains why primary constructors can cause handler duplication issues
    *   Provides recommended alternatives: regular classes with constructors, or record types
    *   Updated Best Practices Summary section

### 🐛 Fixed

*   **Notification Handler Duplication (MediatR #1118):** Fixed contravariance-based incorrect handler registration
    *   Handlers for base notification classes (e.g., `INotificationHandler<E1>`) were incorrectly registered for derived types (`E2`)
    *   This caused duplicate handler invocations when publishing derived notifications
    *   New `CanHandleInterface()` method distinguishes between interface and class type arguments
    *   Generic handlers (`INotificationHandler<INotification>`) still work correctly for all notification types
    *   Class-based handlers now only respond to their exact declared type
*   **Nested Generic Behaviors (MediatR #1051):** Fixed `AddOpenBehavior()` failing with nested generic response types
    *   Behaviors like `IPipelineBehavior<TRequest, Result<T>>` or `IPipelineBehavior<TRequest, List<T>>` now work correctly
    *   New `TypeUnifier` class implements type unification algorithm to infer nested type parameters
    *   Automatically closes generic behaviors against concrete request types at registration time
    *   Supports arbitrary nesting depth (e.g., `Result<Dictionary<TKey, List<TValue>>>`)
*   **DI Container Compatibility:** All 8 example projects now work correctly with .NET 10
    *   Autofac, DryIoc, Grace, Lamar, LightInject, Ninject, SimpleInjector, Windsor
*   **Nullable Annotations:** Added `<Nullable>enable</Nullable>` to Windsor and LightInject example projects

---

## [2.1.1] - 2025-12-06

### 📦 Package

*   **Enhanced NuGet Metadata:** Comprehensive package metadata improvements following Microsoft best practices
    *   Added explicit `PackageId`, `AssemblyVersion`, `FileVersion`, and `Product` properties
    *   Added `Title` property for better NuGet.org presentation
    *   Added `NeutralLanguage` (en) for localization support
    *   Added `MinClientVersion` (6.0) for NuGet client compatibility
    *   Enabled `PackageValidation` with baseline version 2.0.0 for API compatibility checks
    *   Enhanced documentation with `GenerateDocumentationFile`
    *   Added deterministic builds and CI build configuration

### 📚 Documentation

*   **Improved Package Documentation:** Enhanced symbol package generation with `.snupkg` format
*   **Source Link Integration:** Better source code navigation and debugging experience

---

## [2.1.0] - 2025-12-01

### ✨ Added

*   **C# 13 Features:** Full adoption of modern C# 9-13 language features for improved code quality and performance
    *   **Collection Expressions (C# 12):** Modernized 21 collection initializations using `[]` syntax and spread operator `..`
    *   **Target-Typed New (C# 9):** Applied to 50+ instantiations for reduced verbosity
    *   **Pattern Matching (C# 9-11):** Enhanced 6 methods with tuple patterns, relational patterns, and or patterns
    *   **ArgumentNullException.ThrowIfNull (C# 11):** Replaced 6 null checks with concise standard validation

### 🔄 Changed

*   **Code Modernization:** Core, samples, and tests now leverage latest C# features
    *   Reduced code by ~80 lines while improving readability
    *   Enhanced pattern matching in `ObjectDetails.cs` and `ServiceRegistrar.cs`
    *   Streamlined collection handling throughout the codebase
*   **Project References:** Fixed project references in samples to support new structure

### 🚀 Performance

*   **Optimized Collection Operations:** Collection expressions provide compiler optimizations
*   **Improved Pattern Matching:** More efficient than traditional if/else chains
*   **Reduced Allocations:** Target-typed new reduces temporary allocations

### 📚 Documentation

*   **Complete Documentation Overhaul:** Restructured entire documentation with 7 comprehensive guides covering all features, patterns, and best practices (127 KB total)
    *   Added structured documentation: Getting Started, Requests & Handlers, Notifications, Behaviors, Configuration, Exception Handling, and Streaming
    *   Removed old scattered documentation files and `docs/examples/` folder
    *   Created `docs/README.md` with complete navigation and learning paths
    *   Updated main README with clear feature highlights and documentation links
    *   All 40+ MediateX features now fully documented with code examples and best practices
    *   **Modernized code examples** in all documentation to use C# 9-13 features (collection expressions, target-typed new, pattern matching)
    *   **Added Minimal API examples** throughout documentation (Getting Started, Streaming) showing modern .NET approach alongside traditional controllers
    *   Included complete Minimal API example with CRUD operations, commands, queries, and events
    *   Updated GitHub repository references from old URLs to correct repository

---

## [2.0.2] - 2025-11-24

### 🔄 Changed

*   **Rebranding:** Renamed `AddMediatR` to `AddMediateX` throughout entire codebase for consistency with project name

### ⚠️ Breaking Changes

*   **Service Registration:** Must update registration calls from `services.AddMediatR(...)` to `services.AddMediateX(...)`

---

## [2.0.1] - 2025-11-24

### 🐛 Fixed

*   **NuGet Package Display:** Fixed README display in NuGet package manager by correctly referencing `README.nuget.md`

---

## [2.0.0] - 2025-11-24

This release marks a significant strategic shift for MediateX, focusing entirely on modern .NET, project simplification, and a clear vision for the future.

### ⚠️ Breaking Changes

*   **Removed Strong-Naming:** The assembly is no longer strong-named. This aligns with modern .NET practices where package integrity is primarily handled by NuGet signing, but is a breaking change for consumers who relied on a strong-named assembly.
*   **Unified NuGet Package:** The logic previously in `MediateX.Contracts` has been merged into the main `MediateX` package. This simplifies dependency management to a single package reference.

### ✨ Added

*   **Modernized `README.md`:** A completely new README that clearly communicates the project's forward-thinking philosophy, modern .NET focus, and future roadmap.

### 🔄 Changed

*   **Refined Project Structure:** Build scripts have been moved into a dedicated `build/` directory, cleaning up the project root for better organization and clarity.
*   **Solidified .NET 9+ Focus:** Officially dropped all considerations for backward compatibility, positioning the library exclusively for .NET 9 and beyond to fully leverage modern runtime and C# features.

---

## [1.0.0] - 2025-11-15

### 🎉 Initial Release

*   **Project Inception:** `MediateX` was established as a direct fork of the highly-respected **MediatR version 12.5.0**.
*   **Core Mission:** The project was created with the specific goal of providing a modernized mediator implementation for .NET 9 and newer versions. This fork sheds legacy constraints to fully embrace the performance and features of the latest .NET platform.
*   Retained all core features and battle-tested patterns from the original MediatR implementation as the foundational baseline.