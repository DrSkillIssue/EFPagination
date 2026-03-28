# Documentation

## Getting Started

- [Installation & Quick Start](getting-started.md) -- Requirements, NuGet install, first query using the fluent `Keyset()` API

## Core Concepts

- [Pagination Patterns](patterns.md) -- First, last, previous, next page queries using the fluent builder chain
- [Prebuilt Definitions](prebuilt-definitions.md) -- Caching pagination definitions for performance
- [API Reference](api-reference.md) -- Full API surface: fluent builder, cursor encoding, sort registries, low-level Paginate, ASP.NET Core integration

## Advanced Topics

- [Database Indexing](indexing.md) -- Composite indexes, deterministic definitions
- [NULL Handling](null-handling.md) -- Computed columns, expression coalescing
- [Loose Typing](loose-typing.md) -- DTOs, projections, anonymous type references with the low-level API
- [Analyzers & Diagnostics](diagnostics.md) -- Build-time warnings and fixes (KP0001-KP0004)
