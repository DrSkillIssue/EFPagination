namespace EFPagination;

/// <summary>
/// Ordered pagination boundary values bound to a specific <see cref="PaginationQueryDefinition{T}"/>.
/// </summary>
/// <typeparam name="T">The entity type for the associated pagination definition.</typeparam>
public sealed class PaginationValues<T>
{
    internal static readonly PaginationValues<T> Empty = new([]);

    internal PaginationValues(object?[] values)
    {
        Values = values;
    }

    /// <summary>
    /// Creates a new <see cref="PaginationValues{T}"/> from the specified ordered values.
    /// Values must be provided in the same order as the columns in the pagination definition.
    /// </summary>
    /// <param name="values">The ordered boundary values.</param>
    /// <returns>A new <see cref="PaginationValues{T}"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
#pragma warning disable CA1000
    public static PaginationValues<T> Create(params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new PaginationValues<T>(values);
    }

    internal object?[] Values { get; }

    /// <summary>
    /// Gets the number of ordered values stored in this instance.
    /// </summary>
    /// <value>The number of definition-ordered boundary values stored in this instance.</value>
    public int Count => Values.Length;
}
