using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Ordered pagination boundary values bound to a specific <see cref="PaginationQueryDefinition{T}"/>.
/// Allocation-free wrapper over the underlying typed bindings array.
/// </summary>
/// <typeparam name="T">The entity type for the associated pagination definition.</typeparam>
public readonly record struct PaginationValues<T>
{
    internal static PaginationValues<T> Empty => default;

    internal PaginationValues(ColumnBinding[] bindings) => Bindings = bindings;

    internal ColumnBinding[]? Bindings { get; init; }

    /// <summary>
    /// Gets the number of ordered boundary values stored in this instance.
    /// </summary>
    /// <value>The number of definition-ordered boundary values, or <c>0</c> when empty.</value>
    public int Count => Bindings?.Length ?? 0;

    /// <summary>
    /// Gets a value indicating whether this instance has no bound values.
    /// </summary>
    /// <value><see langword="true"/> when no boundary values are stored; otherwise <see langword="false"/>.</value>
    public bool IsEmpty => Bindings is null || Bindings.Length == 0;

    /// <summary>
    /// Creates a new <see cref="PaginationValues{T}"/> from the specified ordered values against
    /// the supplied definition. Values must be provided in column order and will be coerced to
    /// each column's CLR type.
    /// </summary>
    /// <param name="definition">The pagination query definition that determines column types and count.</param>
    /// <param name="values">The ordered boundary values. Must match the definition's column count.</param>
    /// <returns>A new <see cref="PaginationValues{T}"/> with the supplied values bound.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> length does not match the definition column count.</exception>
#pragma warning disable CA1000
    public static PaginationValues<T> Create(
        PaginationQueryDefinition<T> definition,
        params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        var columns = definition.Columns;
        if (values.Length != columns.Length)
        {
            throw new ArgumentException(
                $"Expected {columns.Length} values for definition; received {values.Length}.",
                nameof(values));
        }

        var bindings = new ColumnBinding[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var binding = columns[i].CreateBinding();
            columns[i].WriteBindingFromBoxed(values[i], binding);
            bindings[i] = binding;
        }
        return new PaginationValues<T>(bindings);
    }
}
