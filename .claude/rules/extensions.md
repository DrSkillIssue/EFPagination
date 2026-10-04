---
paths:
  - "src/**"
---

# Microsoft.Extensions

Guidance for code that uses dependency injection, options, configuration, logging, hosting and caching. Also apply
`conventions` and `csharp`.

## Dependency Injection

- Scoped services must never be injected into singleton services (captive dependency) — the scoped service would live
  for the application lifetime
- Transient IDisposable services are tracked by the container until scope disposal — prefer scoped lifetime for
  disposable services
- Service registration order matters — later registrations of the same type override earlier ones unless `TryAdd` is
  used
- Decorator patterns where inner and outer service share the same interface must guard against infinite recursion
  during resolution
- Do not inject `IServiceProvider` broadly as a service locator — inject the specific service needed
- `ActivatorUtilities.CreateInstance` bypasses the container registration or factory for the type being created, but it
  still resolves constructor dependencies through the provided `IServiceProvider`
- Singleton services are accessed concurrently — all singleton implementations must be thread-safe

## Options & Configuration

- `IOptions<T>` provides a singleton snapshot — use `IOptionsMonitor<T>` when configuration may change at runtime
- `IOptionsSnapshot<T>` is scoped — do not inject it into singleton services (captive dependency)
- Named options must be resolved correctly — validate that the name parameter flows through the entire options pipeline
- Use `IValidateOptions<T>` for complex cross-property validation that data annotations cannot express
- `ValidateOnStart()` catches configuration errors during startup instead of at first request
- Configuration key lookups are case-insensitive — comparisons over config keys must use
  `StringComparison.OrdinalIgnoreCase`
- The default binder silently skips unrecognized properties — document this when code depends on strict binding
- Binding and validation failures must produce clear error messages identifying the configuration key and expected value
- Changes to options defaults or validation behavior are breaking changes requiring migration guidance

## Logging

- A logger comes from the container, resolved once: a class takes `ILogger<T>` in its constructor and is registered as
  a service, as the minimal-API example of the high-performance logging guide does (a singleton handler whose instance
  method is the endpoint). Never call `ILoggerFactory.CreateLogger` per call or per request.
- `[LoggerMessage]`-attributed methods are `partial`. A `static` one takes the `ILogger` as a parameter; an instance one
  writes through the class's `ILogger` field or primary-constructor parameter (`LoggerMessageGenerator.Parser`).
- Structured log messages must use template placeholders (`{Name}`) — never embed string interpolation, which bypasses
  structured logging
- Log exceptions via the `ILogger` overload that accepts `Exception` as a parameter — do not embed
  `exception.ToString()` in the template
- Never log sensitive data (credentials, tokens, PII, request/response bodies) at any level, including Debug/Trace
- `IsEnabled` checks should guard expensive log message construction when not using source-generated methods
- Hot logging paths (per-request, per-operation) should use `[LoggerMessage]` source generation

## Hosting

- Hosted services start in registration order and stop in reverse order — document dependencies between services
- `BackgroundService.ExecuteAsync` exceptions must be observed and logged — unobserved task exceptions silently crash the
  host
- Graceful shutdown must handle `OperationCanceledException` from the stopping token without logging it as an error
- Two hosts running in the same process must not interfere via static state

## Caching

- Cache keys must incorporate all inputs that affect the cached result, and must be deterministic and stable across
  process restarts for distributed caches
- Verify that key composition does not create collisions for distinct inputs
- Cache miss must be distinguishable from a cached null value
- When a cache entry expires, only one caller should recompute the value while others wait — `HybridCache` provides
  this
- In-memory cache operations must avoid holding locks during expensive value computation
