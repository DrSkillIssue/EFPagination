#pragma warning disable CA1002
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

internal static class KeysetQueryExecutor
{
    public static async Task<CursorPage<T>> ExecuteAsync<T>(
        KeysetQueryBuilder<T> builder,
        int pageSize,
        CancellationToken ct) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var effectivePageSize = pageSize > builder.MaxPageSizeValue
            ? builder.MaxPageSizeValue
            : pageSize;

        var definition = builder.Definition;
        var source = builder.Source;
        var direction = builder.Direction;

        string? sortBy = builder.SortBy;
        int? previousTotalCount = null;
        bool hasCursor;
        PaginationContext<T> context;

        if (builder.CursorString is not null)
        {
            hasCursor = true;
            if (!PaginationCursor.TryDecode(builder.CursorString.AsSpan(), definition,
                    out var values, out _, out var decodedSortBy, out previousTotalCount))
            {
                throw new ArgumentException("Invalid or expired cursor.");
            }

            sortBy ??= decodedSortBy;
            context = source.Paginate(definition, direction, values);
        }
        else if (builder.BoundValues is not null)
        {
            hasCursor = true;
            context = source.Paginate(definition, direction, builder.BoundValues);
        }
        else if (builder.Reference is not null)
        {
            hasCursor = true;
            context = source.Paginate(definition, direction, builder.Reference);
        }
        else
        {
            hasCursor = false;
            context = source.Paginate(definition, direction);
        }

        var items = await context.Query
            .Take(effectivePageSize + 1)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var hasMore = items.Count > effectivePageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        if (direction == PaginationDirection.Backward)
            CollectionsMarshal.AsSpan(items).Reverse();

        var totalCount = builder.ShouldIncludeCount
            ? await source.CountAsync(ct).ConfigureAwait(false)
            : previousTotalCount ?? -1;

        string? nextCursor = null;
        string? previousCursor = null;

        if (items.Count > 0)
        {
            var cursorOptions = new PaginationCursorOptions(
                sortBy,
                totalCount > 0 ? totalCount : null,
                definition.SchemaFingerprint);

            if (hasMore)
                nextCursor = EncodeCursorFromItem(definition, items[^1], cursorOptions);

            if (hasCursor || direction == PaginationDirection.Backward)
                previousCursor = EncodeCursorFromItem(definition, items[0], cursorOptions);
        }

        return new CursorPage<T>(items, nextCursor, previousCursor, totalCount);
    }

    public static async IAsyncEnumerable<List<T>> StreamAsync<T>(
        KeysetQueryBuilder<T> builder,
        int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        if (builder.Direction == PaginationDirection.Backward)
            throw new InvalidOperationException("StreamAsync only supports forward pagination. Use After() instead of Before().");

        var definition = builder.Definition;
        var source = builder.Source;

        PaginationContext<T> firstContext;
        if (builder.CursorString is not null)
        {
            if (!PaginationCursor.TryDecode(builder.CursorString.AsSpan(), definition,
                    out var values, out _))
            {
                throw new ArgumentException("Invalid or expired cursor.");
            }

            firstContext = source.Paginate(definition, PaginationDirection.Forward, values);
        }
        else if (builder.BoundValues is not null)
        {
            firstContext = source.Paginate(definition, PaginationDirection.Forward, builder.BoundValues);
        }
        else if (builder.Reference is not null)
        {
            firstContext = source.Paginate(definition, PaginationDirection.Forward, builder.Reference);
        }
        else
        {
            firstContext = source.Paginate(definition, PaginationDirection.Forward, (object?)null);
        }

        var items = await firstContext.Query.Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        if (items.Count == 0) yield break;
        object reference = items[^1];
        yield return items;

        while (hasMore)
        {
            var context = source.Paginate(definition, PaginationDirection.Forward, reference);
            items = await context.Query.Take(pageSize + 1).ToListAsync(ct).ConfigureAwait(false);
            hasMore = items.Count > pageSize;
            if (hasMore) items.RemoveAt(items.Count - 1);
            if (items.Count == 0) yield break;
            reference = items[^1];
            yield return items;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string EncodeCursorFromItem<T>(
        PaginationQueryDefinition<T> definition,
        object item,
        PaginationCursorOptions options)
    {
        var columns = definition.Columns;
        var values = new ColumnValue[columns.Length];

        for (var i = 0; i < columns.Length; i++)
        {
            values[i] = new ColumnValue(
                columns[i].GetRequiredPropertyNameForColumnValues(),
                columns[i].ObtainValue(item));
        }

        return PaginationCursor.Encode(values, options);
    }
}
