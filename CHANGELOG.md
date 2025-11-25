# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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