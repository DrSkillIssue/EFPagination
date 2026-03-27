#pragma warning disable CS1591
using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace EFPagination;

public sealed class PaginationSortRegistry<T> where T : class
{
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>>.AlternateLookup<ReadOnlySpan<char>> _ascLookup;
    private readonly FrozenDictionary<string, PaginationQueryDefinition<T>>.AlternateLookup<ReadOnlySpan<char>> _descLookup;
    private readonly PaginationQueryDefinition<T> _default;

    public PaginationSortRegistry(
        PaginationQueryDefinition<T> defaultDefinition,
        params ReadOnlySpan<SortField<T>> fields)
    {
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
