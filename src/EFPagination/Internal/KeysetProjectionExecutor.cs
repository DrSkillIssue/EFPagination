using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

internal static class KeysetProjectionExecutor
{
    public static async Task<CursorPage<TOut>> ExecuteAsync<T, TOut>(
        KeysetQueryBuilder<T> builder,
        Expression<Func<T, TOut>> selector,
        int pageSize,
        CancellationToken ct) where T : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var effectivePageSize = pageSize > builder.MaxPageSizeValue ? builder.MaxPageSizeValue : pageSize;
        var resolved = KeysetQueryExecutor.ResolveContext(builder, builder.Direction);
        var sortBy = builder.SortBy ?? resolved.SortBy;

        var shape = ProjectionShapeCache<T, TOut>.Get(builder.Definition);
        var page = await shape.MaterializeAsync(
            resolved.Context.Query, selector, builder.Definition.Columns,
            effectivePageSize + 1, builder.Direction, ct).ConfigureAwait(false);

        var totalCount = builder.ShouldIncludeCount
            ? await GetCountAsync(builder.Source, ct).ConfigureAwait(false)
            : resolved.TotalCount ?? -1;

        var (next, previous) = EncodeCursorPair(
            builder.Definition, page, page.HasMore, resolved.HasInitialReference,
            builder.Direction, sortBy, totalCount);

        return new CursorPage<TOut>(page.ToItemList(), next, previous, totalCount);
    }

    public static async IAsyncEnumerable<List<TOut>> StreamAsync<T, TOut>(
        KeysetQueryBuilder<T> builder,
        Expression<Func<T, TOut>> selector,
        int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        if (builder.Direction == PaginationDirection.Backward)
            throw new InvalidOperationException("StreamAsync only supports forward pagination. Use After() instead of Before().");

        var resolved = KeysetQueryExecutor.ResolveContext(builder, PaginationDirection.Forward);
        var shape = ProjectionShapeCache<T, TOut>.Get(builder.Definition);
        var definition = builder.Definition;
        var columns = definition.Columns;

        var context = resolved.Context;
        while (true)
        {
            var page = await shape.MaterializeAsync(
                context.Query, selector, columns, pageSize + 1, PaginationDirection.Forward, ct).ConfigureAwait(false);
            if (page.Count == 0) yield break;

            var items = page.ToItemList();
            yield return items;

            if (!page.HasMore) yield break;

            var bindings = new ColumnBinding[columns.Length];
            for (var i = 0; i < columns.Length; i++) bindings[i] = columns[i].CreateBinding();
            page.ExtractKeysIntoBindings(page.Count - 1, bindings);

            context = builder.Source.Paginate(definition, PaginationDirection.Forward, new PaginationValues<T>(bindings));
        }
    }

    private static (string? Next, string? Previous) EncodeCursorPair<T, TOut>(
        PaginationQueryDefinition<T> definition,
        ProjectionMaterializedPage<T, TOut> page,
        bool hasMore,
        bool hasInitialReference,
        PaginationDirection direction,
        string? sortBy,
        int totalCount) where T : class
    {
        if (page.Count == 0) return (null, null);

        var options = new PaginationCursorOptions(sortBy, totalCount > 0 ? totalCount : null);

        string? next = null;
        string? previous = null;

        if (hasMore)
            next = EncodeFromIndex(definition, page, page.Count - 1, options);

        if (hasInitialReference || direction == PaginationDirection.Backward)
            previous = EncodeFromIndex(definition, page, 0, options);

        return (next, previous);
    }

    private static string EncodeFromIndex<T, TOut>(
        PaginationQueryDefinition<T> definition,
        ProjectionMaterializedPage<T, TOut> page,
        int index,
        PaginationCursorOptions options) where T : class
    {
        var columns = definition.Columns;
        var bindings = new ColumnBinding[columns.Length];
        for (var i = 0; i < columns.Length; i++) bindings[i] = columns[i].CreateBinding();
        page.ExtractKeysIntoBindings(index, bindings);
        return PaginationCursor.Encode(definition, new PaginationValues<T>(bindings), options);
    }

    private static Task<int> GetCountAsync<T>(IQueryable<T> source, CancellationToken ct)
        => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(source, ct);
}
