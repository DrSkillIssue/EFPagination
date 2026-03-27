# Analyzers & Diagnostics

This library ships with Roslyn analyzers that detect common misuse at build time.

The analyzers apply equally to definitions built inline, prebuilt definitions, and runtime sort registries. If a `SortField<T>` points at a definition containing a nullable pagination column, the diagnostic is reported where that definition is created.

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

### Affected APIs

`KP0001` can be reported from any call site that builds a definition with `PaginationBuilder<T>`:

```cs
PaginationQuery.Build<User>(b => b.Ascending(x => x.NullableDate));

dbContext.Users.Paginate(b => b.Descending(x => x.NullableDate));
```

String-based definitions such as `PaginationQuery.Build<User>("Created", ...)` do not accept expressions, so they are not analyzer entry points. The nullability requirement still applies to the underlying property being used for pagination.

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
