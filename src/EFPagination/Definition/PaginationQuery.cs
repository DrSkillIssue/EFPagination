using System.Collections.Concurrent;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Factory for building reusable <see cref="PaginationQueryDefinition{T}"/> instances.
/// Definitions should be built once and reused across requests for optimal performance.
/// </summary>
public static class PaginationQuery
{
    /// <summary>
    /// Builds a pagination query definition from the specified builder action.
    /// The resulting definition pre-computes column metadata and caches expression tree templates.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builderAction">An action that configures the pagination columns via a <see cref="PaginationBuilder{T}"/>.</param>
    /// <returns>A reusable pagination query definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builderAction"/> is <see langword="null"/>.</exception>
    public static PaginationQueryDefinition<T> Build<T>(
        Action<PaginationBuilder<T>> builderAction)
    {
        ArgumentNullException.ThrowIfNull(builderAction);

        var columns = BuildColumns(builderAction);
        return new PaginationQueryDefinition<T>(columns);
    }

    /// <summary>
    /// Builds a cached pagination query definition from a property name string.
    /// The definition is built once per unique combination of parameters and cached for reuse.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="propertyName">The property name to sort by.</param>
    /// <param name="descending">Whether the primary sort is descending.</param>
    /// <param name="tiebreaker">Optional tiebreaker property name. Defaults to "Id".</param>
    /// <param name="tiebreakerDescending">Whether the tiebreaker sort is descending.</param>
    /// <returns>A cached, reusable pagination query definition.</returns>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> or <paramref name="tiebreaker"/> does not name a public instance property on <typeparamref name="T"/>.</exception>
    /// <exception cref="InvalidOperationException">The definition cache for <typeparamref name="T"/> has exceeded its maximum size. Use <see cref="PaginationSortRegistry{T}"/> for user-controlled sort fields.</exception>
    public static PaginationQueryDefinition<T> Build<T>(
        string propertyName,
        bool descending,
        string? tiebreaker = "Id",
        bool tiebreakerDescending = false)
    {
        var key = (propertyName, descending, tiebreaker, tiebreakerDescending);
        if (DefinitionCache<T>.Cache.TryGetValue(key, out var existing))
            return existing;

        if (DefinitionCache<T>.Cache.Count >= DefinitionCache<T>.MaxSize)
        {
            throw new InvalidOperationException(
                $"PaginationQuery definition cache exceeded {DefinitionCache<T>.MaxSize} entries for type '{typeof(T).Name}'. " +
                "Use PaginationSortRegistry for user-controlled sort fields.");
        }

        return DefinitionCache<T>.Cache.GetOrAdd(key, static k =>
        {
            var (prop, desc, tie, tieDesc) = k;
            var builder = new PaginationBuilder<T>();
            builder.Column(prop, desc);
            if (tie is not null)
                builder.Column(tie, tieDesc);
            return new PaginationQueryDefinition<T>(builder.ColumnsArray);
        });
    }

    /// <summary>
    /// Executes the builder action and returns the resulting columns as an array.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="builderAction">The builder action to execute.</param>
    /// <returns>The configured pagination columns.</returns>
    internal static PaginationColumn<T>[] BuildColumns<T>(
        Action<PaginationBuilder<T>> builderAction)
    {
        var builder = new PaginationBuilder<T>();
        builderAction(builder);

        return builder.ColumnsArray;
    }

    private static class DefinitionCache<T>
    {
        internal const int MaxSize = 256;
        internal static readonly ConcurrentDictionary<(string, bool, string?, bool), PaginationQueryDefinition<T>> Cache = new();
    }
}
