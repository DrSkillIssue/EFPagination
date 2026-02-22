# NULL Handling

Nullable columns are **not supported** in pagination definitions. The library ships with a [Roslyn analyzer](diagnostics.md) that detects this at build time.

## Why?

`NULL` has special semantics in SQL. You cannot compare against it with standard operators — any comparison with `NULL` evaluates to `UNKNOWN`, causing the entire `WHERE` clause to exclude the row. Different databases also sort `NULL` values differently (some treat it as smallest, others as largest), making pagination behavior unpredictable.

## Solution 1: Computed Columns (Recommended)

Create a non-nullable computed column that coalesces `NULL` to a deterministic value, then use that column in the definition.

### Step 1: Add a Computed Property

```cs
public sealed class User
{
    public int Id { get; init; }
    public DateTime? Created { get; init; }       // Nullable for business reasons
    public DateTime CreatedComputed { get; }       // Non-nullable computed column
}
```

### Step 2: Configure in OnModelCreating

The `COALESCE` SQL replaces `NULL` with a sentinel value. Adjust syntax for your database.

**SQLite:**

```cs
modelBuilder.Entity<User>()
    .Property(x => x.CreatedComputed)
    .HasComputedColumnSql("COALESCE(Created, '9999-12-31 00:00:00')");
```

**SQL Server:**

```cs
modelBuilder.Entity<User>()
    .Property(x => x.CreatedComputed)
    // CONVERT is required for deterministic computed columns (needed for indexing).
    .HasComputedColumnSql("COALESCE(Created, CONVERT(datetime2, '9999-12-31', 102))");
```

### Step 3: Index the Computed Column

```cs
modelBuilder.Entity<User>()
    .HasIndex(x => new { x.CreatedComputed, x.Id });
```

### Step 4: Use in the Definition

```cs
PaginationQuery.Build<User>(b => b.Ascending(x => x.CreatedComputed).Ascending(x => x.Id));
```

### Working Example

See the [Nullable sample page](../samples/Pages/Nullable.cshtml) for a complete working example.

## Solution 2: Expression Coalescing

Use the null-coalescing operator directly in the pagination expression:

```cs
PaginationQuery.Build<User>(b =>
    b.Ascending(x => x.Created ?? DateTime.MinValue).Ascending(x => x.Id));
```

This produces the same result as a computed column but **cannot be indexed**, which may impact performance on large tables.

## See Also

- [Analyzers & Diagnostics](diagnostics.md) — `KP0001` warns about nullable columns
- [Database Indexing](indexing.md) — Indexing computed columns
