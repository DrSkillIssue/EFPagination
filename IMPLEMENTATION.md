# EFPagination Implementation Plan

## Feature 1: Built-in Cursor Encode/Decode

### Problem
Every consumer of keyset pagination builds their own cursor format (Base64-JSON, hex Guid, etc.) with their own encode/decode, sort-mismatch detection, and metadata embedding. This is pagination infrastructure that belongs in the library.

### API Surface
```csharp
public static class PaginationCursor
{
    static string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions? options = null);
    static bool TryDecode(ReadOnlySpan<char> encoded, Span<ColumnValue> values, out int written);
}

public readonly record struct PaginationCursorOptions(string? SortBy = null, int? TotalCount = null);
```

### Implementation (informed by dotnet/runtime research)

**Encode path — zero intermediate allocation:**
1. Write JSON via `Utf8JsonWriter` into a stack-allocated `Span<byte>` (256 bytes covers all realistic cursors). `JsonSerializer.SerializeToUtf8Bytes` allocates a `byte[]` for the result and uses `PooledByteBufferWriter` (16KB initial rental) — both wasteful for 50-200 byte cursors.
2. `Base64Url.GetEncodedLength(jsonLength)` computes exact output size.
3. `Base64Url.TryEncodeToChars(utf8Json, charSpan, out written)` encodes in-place. The runtime implementation (`Base64UrlEncoder.cs`) uses SIMD (AVX2/SSSE3/AdvSimd) for the encoding map.
4. `new string(charSpan[..written])` — single allocation: the final string. Everything else is stack or pooled.

**Decode path — zero allocation except the ColumnValue span:**
1. `Base64Url.GetMaxDecodedLength(encoded.Length)` → stackalloc byte buffer.
2. `Base64Url.TryDecodeFromChars(encoded, buffer, out written)` — SIMD-accelerated.
3. `Utf8JsonReader` over the decoded bytes — zero-allocation streaming parser.
4. Write name/value pairs into caller-provided `Span<ColumnValue>`.

**Why not source-generated JsonSerializer:** For a cursor with 1-4 key/value pairs, manual `Utf8JsonWriter`/`Utf8JsonReader` eliminates the converter dispatch loop that even source-generated `JsonSerializerContext` runs. Direct property writes are ~3 instructions per field vs ~15 for the source-gen path.

### Files
- New: `src/EFPagination/PaginationCursor.cs`
- New: `src/EFPagination/PaginationCursorOptions.cs`

---

## Feature 2: Integrated COUNT via Two-Query Batch

### Problem
Consumers run `COUNT(*)` as a separate sequential query after the page query. Two round-trips instead of one.

### Research Finding
EF Core has no window function projection support. `COUNT(*) OVER()` cannot be injected into an `IQueryable` expression tree. `RowNumberExpression` is the only window function, used internally for `.Skip()/.Take()` on SQL Server, not exposed publicly.

SQLite supports `COUNT(*) OVER()` since 3.25.0, but EF Core cannot generate it from LINQ.

### Implementation
Since window functions are not viable through EF Core's LINQ pipeline, the library provides `ExecuteAsync` that runs both queries but offers the consumer control over when the count runs:

```csharp
public readonly record struct KeysetPage<T>(
    List<T> Items,
    bool HasMore,
    int TotalCount);

public static class PaginationExecutor
{
    static async Task<KeysetPage<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        PaginationQueryDefinition<T> definition,
        int pageSize,
        object? reference,
        bool includeCount,
        CancellationToken ct) where T : class;
}
```

When `includeCount: true`, the count query runs. When `false`, `TotalCount` is set to `-1`. Consumers pass `includeCount: cursor == null` to only count on the first page, then embed the count in the cursor for subsequent pages.

The take+1 trim uses `List<T>.RemoveAt(Count - 1)` — confirmed O(1) for the last element. `CollectionsMarshal.SetCount` exists in .NET 10 but performs identical work for the last-element case (bounds check + clear + size decrement). `RemoveAt` is the correct choice.

### Files
- New: `src/EFPagination/PaginationExecutor.cs`
- New: `src/EFPagination/KeysetPage.cs`

---

## Feature 3: Runtime Sort Definition from Property Name String

### Problem
Consumers pre-build 20+ static `PaginationQueryDefinition<T>` fields because the builder only accepts `Expression<Func<T, TColumn>>`. A string-based API eliminates the boilerplate.

### Research Finding
EF Core uses `Expression.Property(parameterExpression, propertyName)` extensively for runtime property access. The `Expression.Property(Expression, string)` overload calls `Type.GetProperty(propertyName)` internally — one reflection call, then creates a `MemberExpression`.

For caching, `(Type, string, bool)` as a `ConcurrentDictionary` key is optimal — `ValueTuple` does not allocate. `Type` is interned by the runtime.

`Compile(preferInterpretation: false)` is the correct choice because the resulting delegate is cached and reused across requests. Full JIT amortizes over many invocations.

### Implementation

```csharp
public static class PaginationQuery
{
    // Existing:
    static PaginationQueryDefinition<T> Build<T>(Action<PaginationBuilder<T>> builderAction);

    // New:
    static PaginationQueryDefinition<T> Build<T>(
        string propertyName,
        bool descending,
        string? tiebreaker = "Id",
        bool tiebreakerDescending = true);
}
```

Internal caching:
```csharp
private static readonly ConcurrentDictionary<(Type, string, bool, string?, bool), object> s_cache = new();

public static PaginationQueryDefinition<T> Build<T>(
    string propertyName, bool descending,
    string? tiebreaker = "Id", bool tiebreakerDescending = true)
{
    var key = (typeof(T), propertyName, descending, tiebreaker, tiebreakerDescending);
    return (PaginationQueryDefinition<T>)s_cache.GetOrAdd(key, static k =>
    {
        var (type, prop, desc, tb, tbDesc) = k;
        return PaginationQuery.Build<object>(b => // uses internal generic dispatch
        {
            // Build expression from string
        });
    });
}
```

The `PaginationBuilder<T>` gets a new internal method:
```csharp
internal PaginationBuilder<T> Column(string propertyName, bool isDescending)
{
    var param = Expression.Parameter(typeof(T), "x");
    var property = Expression.Property(param, propertyName);
    var lambda = Expression.Lambda(property, param);
    // Create PaginationColumn<T, TColumn> via reflection on property.Type
    // Cache the column construction per (Type, propertyName, isDescending)
}
```

### Files
- Modified: `src/EFPagination/PaginationQuery.cs` — new `Build` overload
- Modified: `src/EFPagination/PaginationBuilder.cs` — new `Column(string, bool)` method

---

## Feature 4: Sort Field Registry

### Problem
Consumers build `FrozenDictionary<string, SortVariant<T>>` manually to validate sort field names and map to definitions. Universal boilerplate.

### Research Finding
`FrozenDictionary<string, T>` with `StringComparer.OrdinalIgnoreCase` uses `KeyAnalyzer` to find the minimal unique substring across keys, then dispatches to specialized implementations (`LeftJustifiedSingleChar`, `LeftJustifiedSubstring`, etc.). For 5 sort fields like `["id", "ipAddress", "createdAt", "notes", "createdBy"]`, it picks a single-character or 2-char substring hash. Lookups are O(1) with a fast length pre-check.

`FrozenDictionary` supports `AlternateLookup<ReadOnlySpan<char>>` — lookups without allocating a string from query parameters. `StringComparer.OrdinalIgnoreCase` implements `IAlternateEqualityComparer<ReadOnlySpan<char>, string>` in .NET 9+.

### Implementation

```csharp
public sealed class PaginationSortRegistry<T> where T : class
{
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>> _ascending;
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>> _descending;
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>>.AlternateLookup<ReadOnlySpan<char>> _ascLookup;
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>>.AlternateLookup<ReadOnlySpan<char>> _descLookup;
    private readonly PaginationQueryDefinition<T> _default;

    public PaginationSortRegistry(
        PaginationQueryDefinition<T> defaultDefinition,
        params ReadOnlySpan<SortField<T>> fields);

    public PaginationQueryDefinition<T> Resolve(ReadOnlySpan<char> sortBy, ReadOnlySpan<char> sortDir);
}

public readonly record struct SortField<T>(
    string Name,
    PaginationQueryDefinition<T> Ascending,
    PaginationQueryDefinition<T> Descending) where T : class;
```

`Resolve` uses the `AlternateLookup<ReadOnlySpan<char>>` to find the definition without allocating a string from the query parameter. Falls back to the default definition for unknown fields.

### Files
- New: `src/EFPagination/PaginationSortRegistry.cs`
- New: `src/EFPagination/SortField.cs`
