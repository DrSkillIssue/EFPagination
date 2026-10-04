---
paths:
  - "**/*.cs"
---

# C# (managed code)

Conventions for C# changes. Also apply `conventions` (all source changes) and `extensions` (code that uses
`Microsoft.Extensions.*`). Test code also follows the test rules in [`AGENTS.md`](/AGENTS.md); where they conflict
with this file, `AGENTS.md` wins. `src/EFPagination.Analyzers` targets `netstandard2.0`, which lacks the `ThrowIf`
helpers and other recent APIs; there, use the closest equivalent the target ships.

## Correctness & Safety

### Error Handling & Assertions

- **Use `Debug.Assert` for internal invariants, not exceptions.** For internal-only callers, assert assumptions rather
  than throwing `ArgumentException`. Prefer `Debug.Assert(value is not null)` over the null-forgiving operator (`!`).
- **Use `throw` for reachable error paths, `UnreachableException` for exhaustive switches.** When a code path might be
  hit at runtime, throw an exception rather than asserting. Use `throw new UnreachableException()` for default cases in
  exhaustive switches.
- **Include actionable details in exception messages.** Use `nameof` for parameter names. Include the unsupported type
  or unexpected value. Never throw empty exceptions.
- **Initialize output parameters in all code paths.** When a method has `out` parameters, ensure they are initialized
  to a defined value in all error paths.
- **Use `ThrowIf` helpers over manual checks.** Use `ArgumentOutOfRangeException.ThrowIfNegative`,
  `ObjectDisposedException.ThrowIf`, etc. instead of manual if-then-throw patterns.
- **Don't swallow exceptions that mask unexpected errors.** Before adding a try/catch that silently discards exceptions
  (`catch { continue; }`, `catch { return null; }`), establish that the exception is a truly expected, recoverable
  condition rather than an unexpected error signaling a deeper problem. Silently catching exceptions that "shouldn't
  happen" hides root causes and makes debugging harder. Let unexpected exceptions propagate or fail fast so the real
  issue gets investigated.

### Thread Safety

- **Use `Volatile` or `Interlocked` for cross-thread field access.** Fields written on one thread and read on another
  must use `Volatile.Read/Write` or `Interlocked`. The `??=` operator is not thread-safe. `Nullable<T>` is not safe for
  caching (two-field struct tears). Do not use shared mutable arrays without synchronization.
- **Use `TickCount64` for timeout calculations.** Use `Environment.TickCount64` (long) instead of
  `Environment.TickCount` (int) to avoid integer overflow.

### Security

- **Guard integer arithmetic against overflow before mutating state.** Guard size computations with checked arithmetic
  or an explicit bounds check. A `checked` expression is sufficient only when it throws before partial state mutation.
  When a guard is separated from the arithmetic it protects, add a brief comment connecting them.
- **Clean sensitive cryptographic data after use.** Clear key material after use. Use non-short-circuit operators (`|`)
  in verification code to prevent timing leaks.
- **Don't proactively send credentials without opt-in.** Never send authentication credentials (especially Basic auth)
  before receiving a challenge.
- **Limit `stackalloc` to ~1KB total per method and validate size.** Don't stackalloc based on user-controlled or large
  input sizes. Move stackalloc to just before usage, not before early returns. Use the bounded pattern
  `(length > Threshold) ? stackalloc[Threshold] : ArrayPool.Rent(length)` to safely cap user input.

### Correctness Patterns

- **Prefer safe code over unsafe micro-optimizations.** Do not introduce `Unsafe.As`, `Unsafe.AsRef`, or raw pointers
  without demonstrable performance need. Prefer Span-based APIs.
- **Seal classes when `Equals` uses exact type matching.** If a class implements `Equals` with `GetType()` comparison,
  treat this as a potential bug when the class is unsealed. Sealing is usually the right fix, but not always — evaluate
  rather than applying it reflexively.

## Performance & Allocations

### Measurement & Evidence

- **Avoid premature optimization with object pools and caches.** Do not introduce global caches or object pools without
  evidence they are needed. Prefer making the underlying operation faster.

### Allocation Avoidance

- **Avoid closures and allocations in hot paths.** When a lambda captures locals creating a closure, consider using a
  static delegate with a state parameter. Avoid string concatenation; use span-based operations.
- **Pre-allocate collections when size is known.** Pass capacity to `Dictionary`, `HashSet`, `List` constructors when
  the expected count is available.
- **Structs in dictionaries need `IEquatable<T>` and `GetHashCode`.** Without these, the runtime falls back to boxing
  allocations for equality comparison.

### Code Structure for Performance

- **Place cheap checks before expensive operations.** Order conditionals so cheapest/most-common checks come first.
  Move expensive work after early-exit checks.
- **Allocate resources lazily where possible.** Allocate expensive resources on first use, not during initialization.
- **Avoid O(n²) patterns in collections and hot paths.** Watch for linear scans inside loops, repeated `RemoveAt` in
  loops. Use `RemoveAll`, single-pass restructuring, or appropriate data structures.
- **Cache repeated accessor calls in locals.** Store the result of repeated property/getter calls in a local variable.
- **Consider scalability, not just throughput.** Evaluate whether data structures, caches, and locking strategies will
  hold up at high cardinality or under concurrent load. Watch for unbounded collection growth, lock contention that
  worsens with core count, and O(1) assumptions that break at scale.

## API Design & Contracts

- **Align exception types and validation order.** Validate arguments first (`ArgumentNullException`, then
  `ArgumentException`), then `ObjectDisposedException`, then perform the operation.
- **`Try` APIs should return `false` only for the common expected failure.** Throw for everything else (corruption,
  permissions, invalid arguments). Try methods must always throw on invalid arguments.
- **Don't expose mutable options after construction.** If values are captured at construction time, don't expose a
  mutable options object.
- **New virtual methods must work with unoverridden derived types.** The default implementation must behave identically
  to calling the pre-existing equivalent APIs.
- **Use named types instead of `ValueTuple` across file boundaries.**

## Code Style & Formatting

- **Use well-named constants instead of magic numbers.** No raw hex or decimal constants without explanation. Don't
  duplicate magic constants across files.
- **Use PascalCase for constants; descriptive names for booleans.** All constant locals and fields use PascalCase.
  Boolean fields should be positive and descriptive (`_hasCurrent` not `valid`).
- **Name methods to accurately reflect their behavior.** Update names when behavior changes. `Get*` implies a return
  value; use `Print*/Display*` for void. `ThrowIf` not `ThrowExceptionIf`.
- **Prefer early return to reduce nesting.** Use early returns for short/error cases to avoid unnecessary nesting. Put
  the error case first, success return last.
- **Avoid `using static` and `#region` in new code.** `using static` is costly when reading code outside IDEs (e.g.,
  GitHub review). `#region` gets out of date quickly.
- **Place local functions at method end, fields first in types.** Local functions go at the end of the containing
  method. Fields are the first members declared in a type.
- **Narrow warning suppression to smallest scope.** Avoid file-wide `#pragma` suppressions. Disable only around the
  specific line that triggers the warning.
- **Use pattern matching and `is`/`or`/`and` patterns.** Prefer `is` patterns and C# pattern matching over manual type
  checks and comparisons. Use named parameters for boolean arguments.
- **Do not initialize fields to default values (CA1805).** Explicit `= false`, `= 0`, `= null` is redundant.
- **Sealed classes do not need the full Dispose pattern.** A simple `Dispose()` is sufficient since no derived class can
  introduce a finalizer.
