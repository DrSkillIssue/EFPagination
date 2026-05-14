using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

/// <summary>
/// Backs <see cref="KeysetQueryBuilder{T}.TakeAsync(int, CancellationToken)"/> and
/// <see cref="KeysetQueryBuilder{T}.StreamAsync(int, CancellationToken)"/>. Resolves the
/// builder's accumulated state to a concrete <see cref="PaginationContext{T}"/>, materializes
/// the page, and assembles the resulting <see cref="CursorPage{T}"/>.
/// </summary>
internal static class KeysetQueryExecutor
{
    /// <summary>
    /// Executes the builder as a one-shot paginated query.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The accumulated builder state.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="CursorPage{T}"/> with items, cursor tokens, and optional total count.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="ArgumentException">The builder's cursor string is invalid or expired.</exception>
    public static async Task<CursorPage<T>> ExecuteAsync<T>(
        KeysetQueryBuilder<T> builder,
        int pageSize,
        CancellationToken ct) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var effectivePageSize = pageSize > builder.MaxPageSizeValue ? builder.MaxPageSizeValue : pageSize;

        var resolved = ResolveContext(builder, builder.Direction);
        var sortBy = builder.SortBy ?? resolved.SortBy;

        var (items, hasMore) = await PageMaterializer.MaterializeAsync(
            resolved.Context.Query, effectivePageSize, builder.Direction, ct).ConfigureAwait(false);

        var totalCount = builder.ShouldIncludeCount
            ? await builder.Source.CountAsync(ct).ConfigureAwait(false)
            : resolved.TotalCount ?? -1;

        var (next, previous) = CursorPair.Encode(
            builder.Definition, items, hasMore, resolved.HasInitialReference, builder.Direction, sortBy, totalCount);

        return new CursorPage<T>(items, next, previous, totalCount);
    }

    /// <summary>
    /// Executes the builder as a streaming forward enumeration of pages.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The accumulated builder state.</param>
    /// <param name="pageSize">The requested page size per batch.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async enumerable yielding one materialized page per iteration.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The builder is configured for backward pagination.</exception>
    public static async IAsyncEnumerable<List<T>> StreamAsync<T>(
        KeysetQueryBuilder<T> builder,
        int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        if (builder.Direction == PaginationDirection.Backward)
            throw new InvalidOperationException("StreamAsync only supports forward pagination. Use After() instead of Before().");

        var resolved = ResolveContext(builder, PaginationDirection.Forward);

        await foreach (var page in PaginationStreaming.StreamForwardAsync(
            builder.Source, builder.Definition, resolved.Context, pageSize, ct).ConfigureAwait(false))
        {
            yield return page;
        }
    }

    /// <summary>
    /// The resolved inputs to the materialization phase.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="Context">The ordered, optionally filtered query.</param>
    /// <param name="SortBy">The logical sort key recovered from the incoming cursor, if any.</param>
    /// <param name="TotalCount">The total count recovered from the incoming cursor, if any.</param>
    /// <param name="HasInitialReference">Whether the request supplied a cursor, reference object, or bound values.</param>
    internal readonly record struct ResolvedContext<T>(
        PaginationContext<T> Context,
        string? SortBy,
        int? TotalCount,
        bool HasInitialReference) where T : class;

    /// <summary>
    /// Maps the builder's accumulated state to a concrete <see cref="ResolvedContext{T}"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builder">The accumulated builder state.</param>
    /// <param name="direction">The pagination direction to apply.</param>
    /// <returns>The resolved inputs for the materialization phase.</returns>
    /// <exception cref="ArgumentException">The builder's cursor string is invalid or expired.</exception>
    internal static ResolvedContext<T> ResolveContext<T>(
        in KeysetQueryBuilder<T> builder,
        PaginationDirection direction) where T : class
    {
        var definition = builder.Definition;
        var source = builder.Source;

        if (builder.CursorString is not null)
        {
            if (!PaginationCursor.TryDecode(builder.CursorString.AsSpan(), definition, out var values, out var metadata))
                throw new ArgumentException("Invalid or expired cursor.");
            return new ResolvedContext<T>(
                source.Paginate(definition, direction, values),
                metadata.SortBy, metadata.TotalCount, HasInitialReference: true);
        }

        if (!builder.BoundValues.IsEmpty)
            return new ResolvedContext<T>(
                source.Paginate(definition, direction, builder.BoundValues),
                null, null, HasInitialReference: true);

        if (builder.Reference is not null)
            return new ResolvedContext<T>(
                source.Paginate(definition, direction, builder.Reference),
                null, null, HasInitialReference: true);

        return new ResolvedContext<T>(
            source.Paginate(definition, direction),
            null, null, HasInitialReference: false);
    }
}
