using System.Runtime.CompilerServices;
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

    internal readonly record struct ResolvedContext<T>(
        PaginationContext<T> Context,
        string? SortBy,
        int? TotalCount,
        bool HasInitialReference) where T : class;

    private static ResolvedContext<T> ResolveContext<T>(
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
