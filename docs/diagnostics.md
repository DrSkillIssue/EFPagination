# Analyzers & Diagnostics

This library ships with Roslyn analyzers that detect common misuse at build time.

## Diagnostic Reference

| ID | Severity | Title | Description |
|----|----------|-------|-------------|
| `KP0001` | Error | Pagination column may be null | A pagination column expression resolves to a nullable type, which is unsupported. |

## KP0001

**Definition contains a nullable column.**

Nullable columns cannot be used in pagination definitions because `NULL` comparisons produce unpredictable results in SQL. The analyzer flags `Ascending` and `Descending` calls where the column expression's return type is nullable.

### Examples That Trigger This Diagnostic

```cs
// Nullable property:
b.Descending(x => x.NullableDate);          // KP0001

// Nullable navigation chain:
b.Ascending(x => x.NullableDetails.Id);     // KP0001
```

### How to Fix

**Option 1: Use a computed column** (recommended for production):

```cs
b.Ascending(x => x.NullableDateComputed);   // Non-nullable computed column
```

See [NULL Handling](null-handling.md#solution-1-computed-columns-recommended) for setup instructions.

**Option 2: Use null-coalescing in the expression**:

```cs
b.Ascending(x => x.NullableDate ?? DateTime.MinValue);   // No diagnostic
```

See [NULL Handling](null-handling.md#solution-2-expression-coalescing) for trade-offs.

### Suppressing the Diagnostic

If you have a valid reason to suppress (e.g., you guarantee the column is never null in practice):

```cs
#pragma warning disable KP0001
b.Ascending(x => x.NullableDate);
#pragma warning restore KP0001
```

Or in your `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.KP0001.severity = none
```

## See Also

- [NULL Handling](null-handling.md) — Full explanation of the problem and solutions

