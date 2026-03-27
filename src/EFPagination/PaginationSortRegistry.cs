using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace EFPagination;

/// <summary>
/// Resolves external sort field and direction values to prebuilt pagination definitions.
/// </summary>
/// <typeparam name="T">The entity type handled by the registry.</typeparam>
public sealed class PaginationSortRegistry<T> where T : class
{
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>>.AlternateLookup<ReadOnlySpan<char>> _ascLookup;
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>>.AlternateLookup<ReadOnlySpan<char>> _descLookup;
    private readonly PaginationQueryDefinition<T> _default;

    /// <summary>
    /// Initializes a new sort registry.
    /// </summary>
    /// <param name="defaultDefinition">The fallback definition used when no matching sort field is found.</param>
    /// <param name="fields">The sortable fields exposed by the registry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="defaultDefinition"/> is <see langword="null"/>.</exception>
    public PaginationSortRegistry(
        PaginationQueryDefinition<T> defaultDefinition,
        params ReadOnlySpan<SortField<T>> fields)
    {
        ArgumentNullException.ThrowIfNull(defaultDefinition);
        _default = defaultDefinition;

        var ascDict = new Dictionary<string, PaginationQueryDefinition<T>>(fields.Length, StringComparer.OrdinalIgnoreCase);
        var descDict = new Dictionary<string, PaginationQueryDefinition<T>>(fields.Length, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < fields.Length; i++)
        {
            ascDict[fields[i].Name] = fields[i].Ascending;
            descDict[fields[i].Name] = fields[i].Descending;
        }

        var ascending = ascDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        var descending = descDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _ascLookup = ascending.GetAlternateLookup<ReadOnlySpan<char>>();
        _descLookup = descending.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Resolves the pagination definition for the requested sort field and direction.
    /// </summary>
    /// <param name="sortBy">The requested logical sort field name.</param>
    /// <param name="sortDir">The requested direction; <c>desc</c> selects descending, all other values select ascending.</param>
    /// <returns>The matched pagination definition, or the default definition when no field matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PaginationQueryDefinition<T> Resolve(ReadOnlySpan<char> sortBy, ReadOnlySpan<char> sortDir)
    {
        if (sortBy.IsEmpty)
            return _default;

        var isDesc = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var lookup = isDesc ? _descLookup : _ascLookup;

        return lookup.TryGetValue(sortBy, out var definition) ? definition : _default;
    }
}
