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

    internal object?[] Values { get; }

    /// <summary>
    /// Gets the number of ordered values stored in this instance.
    /// </summary>
    public int Count => Values.Length;
}
