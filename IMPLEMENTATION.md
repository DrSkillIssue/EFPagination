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

### Implementation

**Encode path:**
1. Write JSON via `Utf8JsonWriter` into a stack-allocated `Span<byte>`. If the cursor exceeds the stack buffer, fall back to `ArrayPool<byte>`. `JsonSerializer.SerializeToUtf8Bytes` internally uses `PooledByteBufferWriter` with a 16KB default initial buffer (`JsonSerializerOptions.DefaultBufferSize`, confirmed at `src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializerOptions.cs:321`) and always allocates a final `byte[]` via `WrittenSpan.ToArray()` — disproportionate for small cursor payloads.
2. `Base64Url.GetEncodedLength(jsonLength)` computes the exact output character count (`src/libraries/System.Private.CoreLib/src/System/Buffers/Text/Base64Url/Base64UrlEncoder.cs:40`).
3. `Base64Url.TryEncodeToChars(utf8Json, charSpan, out written)` encodes into a caller-provided char span. The runtime implementation has SIMD-specific code paths for AVX2 and AdvSimd (`Base64UrlEncoder.cs:107, :296, :307, :330`).
4. `new string(charSpan[..written])` — the only heap allocation in the encode path.

**Decode path:**
1. `Base64Url.GetMaxDecodedLength(encoded.Length)` computes the maximum decoded byte count (`Base64UrlDecoder.cs:29`). Stackalloc for small cursors, `ArrayPool` rent for larger.
2. `Base64Url.TryDecodeFromChars(encoded, buffer, out written)` — SIMD-accelerated decode paths for AdvSimd, Ssse3, and Avx2 (`Base64UrlDecoder.cs:241, :376, :413`).
3. `Utf8JsonReader` over the decoded bytes — a `ref struct`, forward-only, zero-allocation streaming parser (`src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.cs:10`).
4. Write name/value pairs into caller-provided `Span<ColumnValue>`.

**Why manual Utf8JsonWriter over source-generated JsonSerializer:** For small fixed-schema JSON (1-4 fields), manual `Utf8JsonWriter.WriteString`/`WriteNumber` calls avoid the source-generated converter's property-loop dispatch. The performance difference has not been benchmarked for this exact shape — the recommendation is based on the code path difference (direct writes vs. converter resolution + property iteration), not measured instruction counts. Both are valid; manual writes are used here because the schema is fixed and known at compile time.

### Files
- New: `src/EFPagination/PaginationCursor.cs`
- New: `src/EFPagination/PaginationCursorOptions.cs`

---

## Feature 2: Integrated COUNT via Two-Query Execution

### Problem
Consumers run `COUNT(*)` as a separate sequential query after the page query. Two database round-trips instead of one.

### Research Finding
EF Core has no general window function projection support. `RowNumberExpression` is the only window function, used internally for `.Skip()/.Take()` translation, not exposed publicly. `COUNT(*) OVER()` cannot be injected into an `IQueryable` expression tree via LINQ or `EF.Functions`. This is an EF Core limitation, not a runtime limitation.

SQLite supports `COUNT(*) OVER()` since version 3.25.0 (2018-09-15), but EF Core's LINQ translator cannot generate it.

### Implementation
The library provides `ExecuteAsync` that runs the page query and optionally the count query, giving the consumer control:

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

When `includeCount: false`, `TotalCount` is set to `-1`. Consumers pass `includeCount: cursor == null` to only count on the first page, then embed the count in the cursor for subsequent pages.

**Take+1 trim:** `List<T>.RemoveAt(Count - 1)` is O(1) for the last element — it decrements `_size`, clears the slot for reference types, increments `_version`, and skips `Array.Copy` because `_size - index - 1 == 0` (`src/libraries/System.Private.CoreLib/src/System/Collections/Generic/List.cs:1001`). `CollectionsMarshal.SetCount` performs similar tail-shrink work (`src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/CollectionsMarshal.cs:144`) — it calls `Array.Clear` on truncated slots and sets `_size` — but the two methods are not identical in implementation. `RemoveAt` is used here because it is the established pattern and has equivalent performance for the last-element case.

### Files
- New: `src/EFPagination/PaginationExecutor.cs`
- New: `src/EFPagination/KeysetPage.cs`

---

## Feature 3: Runtime Sort Definition from Property Name String

### Problem
Consumers pre-build 20+ static `PaginationQueryDefinition<T>` fields because the builder only accepts `Expression<Func<T, TColumn>>`. A string-based API eliminates the boilerplate.

### Research Finding
`Expression.Property(Expression, string)` performs up to two reflective property lookups with binding flags (including `IgnoreCase`), then creates a `MemberExpression` via the `Property(expression, PropertyInfo)` overload (`src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/MemberExpression.cs:206`). This reflection cost is paid once per definition build, not per request, because the resulting `PaginationQueryDefinition<T>` is cached.

`Compile(preferInterpretation: false)` (the default) produces a full JIT-compiled delegate. This is the correct choice when the delegate is built once and invoked many times — the upfront JIT cost amortizes over repeated invocations. EF Core uses `preferInterpretation: true` for expressions evaluated during query translation (where startup speed matters more), and `false` for resolver delegates that persist across requests (`LiftableConstantProcessor.cs:200`, `RelationalLiftableConstantProcessor.cs:40`).

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

Internal caching via `ConcurrentDictionary` with a `ValueTuple` key:
```csharp
private static readonly ConcurrentDictionary<(Type, string, bool, string?, bool), object> s_cache = new();

public static PaginationQueryDefinition<T> Build<T>(
    string propertyName, bool descending,
    string? tiebreaker = "Id", bool tiebreakerDescending = true)
{
    var key = (typeof(T), propertyName, descending, tiebreaker, tiebreakerDescending);
    return (PaginationQueryDefinition<T>)s_cache.GetOrAdd(key, static k =>
    {
        // Build PaginationQueryDefinition<T> from string property names
    });
}
```

The `ValueTuple` key is a value type and does not allocate on the heap for the key itself. However, `ConcurrentDictionary` boxes value-type keys internally via `IEqualityComparer<TKey>.GetHashCode(TKey)` and stores them in `Node` objects — the "zero allocation" claim applies to the lookup path (no key allocation per `TryGetValue`), not the insertion path.

The `PaginationBuilder<T>` gets a new internal method:
```csharp
internal PaginationBuilder<T> Column(string propertyName, bool isDescending)
{
    var param = Expression.Parameter(typeof(T), "x");
    var property = Expression.Property(param, propertyName);
    var lambda = Expression.Lambda(property, param);
    // Create PaginationColumn<T, TColumn> via MakeGenericType on property.PropertyType
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
`FrozenDictionary<string, T>` with `StringComparer.OrdinalIgnoreCase` runs `KeyAnalyzer.Analyze` (`src/libraries/System.Collections.Immutable/src/System/Collections/Frozen/String/KeyAnalyzer.cs:17`) to find the minimal unique substring across all keys, then selects from ~12 specialized implementations such as `OrdinalStringFrozenDictionary_LeftJustifiedSingleChar` or `_LeftJustifiedSubstring` (`FrozenDictionary.cs:224, :232`). The exact implementation chosen depends on the specific key set at construction time — it is not predictable without running the analyzer on the actual keys.

`FrozenDictionary` supports `AlternateLookup<ReadOnlySpan<char>>` for string keys (`FrozenDictionary.AlternateLookup.cs:24, :45, :52`). `StringComparer.OrdinalIgnoreCase` implements `IAlternateEqualityComparer<ReadOnlySpan<char>, string?>` (`src/libraries/System.Private.CoreLib/src/System/StringComparer.cs:29, :321, :380`). This enables lookups from query parameter spans without allocating a `string` for the key.

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

`Resolve` uses the `AlternateLookup<ReadOnlySpan<char>>` to look up the definition from a query parameter span. Returns the default definition for unknown sort fields — the library does not throw on invalid input.

### Files
- New: `src/EFPagination/PaginationSortRegistry.cs`
- New: `src/EFPagination/SortField.cs`
