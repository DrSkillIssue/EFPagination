using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Represents the ordered key columns of a keyset-paginated query.
/// </summary>
/// <remarks>
/// Build it once with <see cref="PaginationQuery.Build{T}(Action{PaginationBuilder{T}})"/> and reuse it across
/// requests. A cursor decodes only with a definition of the same column names, types and directions, in any process.
/// </remarks>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PaginationQueryDefinition<T>
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    internal PaginationQueryDefinition(
        PaginationColumn<T>[] columns)
    {
        Columns = columns;
        PredicateTemplate = FilterPredicateStrategy.CreateTemplate(columns);

        // Any process may decode a cursor, so the hash is FNV-1a (draft-eastlake-fnv): string.GetHashCode is seeded
        // per process (Marvin.DefaultSeed). Type.FullName names generic arguments with their assembly version;
        // ToString() does not. A computed column has no property name, so its expression's structure stands in for it.
        var hash = FnvOffsetBasis;
        foreach (var column in columns)
        {
            hash = Append(hash, column.PropertyName ?? ColumnShape.Write(column.LambdaExpression));
            hash = Append(hash, column.Type.ToString());
            hash = (hash ^ (column.IsDescending ? 1u : 0u)) * FnvPrime;
        }
        SchemaFingerprint = hash;

        // The length goes first, so that ("ab", "c") and ("a", "bc") hash apart.
        static uint Append(uint hash, string? value)
        {
            hash = (hash ^ (uint)(value?.Length ?? 0)) * FnvPrime;
            foreach (var c in value.AsSpan())
                hash = (hash ^ c) * FnvPrime;
            return hash;
        }
    }

    internal PaginationColumn<T>[] Columns { get; }

    internal CachedPredicateTemplate<T> PredicateTemplate { get; }

    internal int ColumnCount => Columns.Length;

    internal uint SchemaFingerprint { get; }
}
