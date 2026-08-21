using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Backs the server-side projection overloads of
/// <see cref="KeysetQueryBuilder{T}.TakeAsync{TOut}(int, Expression{Func{T, TOut}}, CancellationToken)"/>
/// and <see cref="KeysetQueryBuilder{T}.StreamAsync{TOut}(int, Expression{Func{T, TOut}}, CancellationToken)"/>.
/// Wraps the user's projection in an arity-specialized envelope so EF Core emits a single
/// <c>SELECT</c> covering the projected columns and the keyset key columns simultaneously.
/// </summary>
internal static class KeysetProjectionExecutor
{
    /// <summary>
    /// Executes the builder with a server-side projection as a one-shot paginated query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="builder">The accumulated builder state.</param>
    /// <param name="selector">The server-translatable projection.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="CursorPage{TOut}"/> with projected items and cursor tokens.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="NotSupportedException">The pagination definition has fewer than 1 or more than 8 key columns.</exception>
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

        var totalCount = await TotalCountResolver
            .ResolveAsync(builder.ShouldIncludeCount, resolved.TotalCount, builder.Source, ct)
            .ConfigureAwait(false);

        string? next = null;
        string? previous = null;
        if (page.Count > 0)
        {
            var options = new PaginationCursorOptions(sortBy, PaginationCount.AsNullable(totalCount));

            var hasPrevious = builder.Direction == PaginationDirection.Forward ? resolved.HasInitialReference : page.HasMore;
            var hasNext = builder.Direction == PaginationDirection.Forward ? page.HasMore : resolved.HasInitialReference;

            if (hasNext)
                next = EncodeFromIndex(builder.Definition, page, page.Count - 1, options);

            if (hasPrevious)
                previous = EncodeFromIndex(builder.Definition, page, 0, options);
        }

        return new CursorPage<TOut>(page.ToItemList(), next, previous, totalCount);
    }

    /// <summary>
    /// Executes the builder with a server-side projection as a streaming forward enumeration of pages.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TOut">The projected DTO type.</typeparam>
    /// <param name="builder">The accumulated builder state.</param>
    /// <param name="selector">The server-translatable projection.</param>
    /// <param name="pageSize">The requested page size per batch.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async enumerable yielding one projected page per iteration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The builder is configured for backward pagination.</exception>
    /// <exception cref="NotSupportedException">The pagination definition has fewer than 1 or more than 8 key columns.</exception>
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
}
