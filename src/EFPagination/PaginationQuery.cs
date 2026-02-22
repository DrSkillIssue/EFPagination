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
}
