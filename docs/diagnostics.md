# Analyzers & Diagnostics

This library ships with Roslyn analyzers that detect common misuse at build time.

The analyzers apply equally to prebuilt definitions and runtime sort registries. If a `SortField<T>` points at a definition containing a nullable pagination column, the diagnostic is reported where that definition is created.

## Diagnostic Reference

| ID | Severity | Title | Description |
|----|----------|-------|-------------|
| `KP0001` | Error | Pagination column may be null | A pagination column expression resolves to a nullable type, which is unsupported. |
| `KP0002` | Warning | Non-unique tiebreaker | The last column in the definition is not unique, which can cause rows to be skipped. |
| `KP0003` | Info | Ad-hoc builder in hot path | A `PaginationQuery.Build<T>(b => ...)` call appears inside a method body rather than a static field. |
| `KP0004` | Warning | Missing order correction after backward | After a backward `Paginate` call, `EnsureCorrectOrder` was not called before use. |

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

## KP0002

**Non-unique tiebreaker.**

The last column in the pagination definition should be unique to ensure deterministic ordering. When it is not unique, rows with identical values in all pagination columns may be skipped or duplicated across pages.

### How to Fix

Append a unique column (typically the primary key) as the last column:

```cs
// Before (non-deterministic):
PaginationQuery.Build<User>(b => b.Descending(x => x.Created));

// After (deterministic):
PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

## KP0003

**Ad-hoc builder in hot path.**

A `PaginationQuery.Build<T>(b => ...)` call appears inside a method body rather than being stored as a static field. Each call creates a new definition with fresh expression tree compilation. For best performance, store definitions as `static readonly` fields.

### How to Fix

```cs
// Before (allocates per call):
public async Task<CursorPage<User>> GetUsersAsync(string? cursor)
{
    var definition = PaginationQuery.Build<User>(b => b.Ascending(x => x.Id));
    return await dbContext.Users.Keyset(definition).TakeAsync(20);
}

// After (zero per-call overhead):
private static readonly PaginationQueryDefinition<User> Definition =
    PaginationQuery.Build<User>(b => b.Ascending(x => x.Id));

public async Task<CursorPage<User>> GetUsersAsync(string? cursor)
{
    return await dbContext.Users.Keyset(Definition).TakeAsync(20);
}
```

## KP0004

**Missing order correction after backward pagination.**

When using the low-level `Paginate` API with `PaginationDirection.Backward`, the results come back in reverse order. You must call `EnsureCorrectOrder` before using the data. The fluent `Keyset()` API handles this automatically.

### How to Fix

```cs
var context = dbContext.Users.Paginate(definition, PaginationDirection.Backward, reference);

var users = await context.Query.Take(20).ToListAsync();
context.EnsureCorrectOrder(users); // Required for backward pagination
```

Or use `MaterializeAsync`, which handles order correction internally:

```cs
var page = await dbContext.Users
    .Paginate(definition, PaginationDirection.Backward, reference)
    .MaterializeAsync(20);
```

Or use the fluent API, which always returns items in correct order:

```cs
var page = await dbContext.Users
    .Keyset(definition)
    .Before(cursor)
    .TakeAsync(20);
```

## See Also

- [NULL Handling](null-handling.md) -- Full explanation of the nullable column problem and solutions
- [Database Indexing](indexing.md) -- Deterministic definitions and composite indexes
- [Prebuilt Definitions](prebuilt-definitions.md) -- Caching definitions for performance
