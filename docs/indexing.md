# Database Indexing

## Why Indexing Matters

Keyset pagination translates to `WHERE` + `ORDER BY` queries. Without a compatible index, the database must scan and sort the entire table on every page request, defeating the purpose of keyset pagination entirely.

## Creating Compatible Indexes

Create a composite index that matches the columns and order of your definition:

```cs
// Pagination definition:
PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));
```

```cs
// Matching index in OnModelCreating:
modelBuilder.Entity<User>()
    .HasIndex(x => new { x.Created, x.Id });
```

> [!NOTE]
> Support for specifying sort order in a composite index was introduced in EF Core 7.0. See the [EF Core documentation on indexes](https://docs.microsoft.com/en-us/ef/core/modeling/indexes) for details.

For mixed-direction definitions, create the index with matching sort directions:

```cs
modelBuilder.Entity<User>()
    .HasIndex(x => new { x.Created, x.Id })
    .IsDescending(true, false); // Created DESC, Id ASC
```

## Deterministic Definitions

A **deterministic definition** uniquely identifies every row. This is critical for correct pagination.

### The Problem

```cs
b.Ascending(x => x.Created)
```

If multiple rows share the same `Created` value, keyset pagination may skip rows when navigating between pages. This is because the `WHERE` clause uses strict inequality — rows with the same cursor value are excluded.

### The Fix

Add columns until the definition is unique. Most commonly, append the primary key:

```cs
b.Ascending(x => x.Created).Ascending(x => x.Id)
```

The combination of `Created` + `Id` is deterministic because `Id` is unique. This guarantees **stable pagination**: no rows are skipped or duplicated across pages.

### When Is This Not Needed?

If the first column is already unique (e.g., an auto-increment `Id`), a single-column definition is deterministic by itself:

```cs
b.Ascending(x => x.Id)
```

## Index Guidance by Definition

| Definition | Index |
|--------|-------|
| `Id ASC` | Index on `Id` (usually the PK) |
| `Created DESC, Id ASC` | Composite index on `(Created DESC, Id ASC)` |
| `Score DESC, Created DESC, Id ASC` | Composite index on `(Score DESC, Created DESC, Id ASC)` |
| `Details.Created DESC, Id ASC` | Composite index on `(Details.Created DESC, Id ASC)` — may require a separate table index for owned types |

## See Also

- [NULL Handling](null-handling.md) — Indexing computed columns for nullable columns
- [Prebuilt Definitions](prebuilt-definitions.md) — Reducing per-query overhead
