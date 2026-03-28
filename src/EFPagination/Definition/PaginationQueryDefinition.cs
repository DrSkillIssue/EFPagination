using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// A prebuilt, reusable pagination query definition. Stores the pagination columns and a cached
/// predicate template for efficient per-call expression tree instantiation.
/// Build once via <see cref="PaginationQuery.Build{T}(Action{PaginationBuilder{T}})"/> and reuse across requests.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PaginationQueryDefinition<T>
{
    internal PaginationQueryDefinition(
        PaginationColumn<T>[] columns)
    {
        Columns = columns;
        PredicateTemplate = FilterPredicateStrategy.Default.CreateTemplate(columns);
        SchemaFingerprint = ComputeFingerprint(columns);
    }

    internal PaginationColumn<T>[] Columns { get; }

    internal CachedPredicateTemplate<T> PredicateTemplate { get; }

    internal int ColumnCount => Columns.Length;

    internal uint SchemaFingerprint { get; }

    private static uint ComputeFingerprint(PaginationColumn<T>[] columns)
    {
        uint hash = 2166136261;
        for (var i = 0; i < columns.Length; i++)
        {
            hash ^= (uint)(columns[i].PropertyName?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash *= 16777619;
            hash ^= (uint)(columns[i].Type.FullName?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash *= 16777619;
            hash ^= columns[i].IsDescending ? 1u : 0u;
            hash *= 16777619;
        }
        return hash;
    }
}
