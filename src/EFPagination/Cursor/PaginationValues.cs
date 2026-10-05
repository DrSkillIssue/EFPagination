using System.Diagnostics;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Represents the boundary values of a page, in the column order of the <see cref="PaginationQueryDefinition{T}"/>
/// that created them.
/// </summary>
/// <typeparam name="T">The entity type for the associated pagination definition.</typeparam>
public readonly record struct PaginationValues<T>
{
    internal PaginationValues(PaginationQueryDefinition<T> definition, ColumnBinding[] bindings)
    {
        Definition = definition;
        Bindings = bindings;
    }

    internal PaginationQueryDefinition<T>? Definition { get; }

    internal ColumnBinding[]? Bindings { get; }

    /// <summary>
    /// Gets the number of ordered boundary values stored in this instance.
    /// </summary>
    /// <value>The number of definition-ordered boundary values, or <c>0</c> when empty.</value>
    public int Count => Bindings?.Length ?? 0;

    /// <summary>
    /// Gets a value that indicates whether this instance holds no values.
    /// </summary>
    /// <value><see langword="true"/> if this instance holds no values; otherwise, <see langword="false"/>.</value>
    public bool IsEmpty => Bindings is null || Bindings.Length == 0;

    /// <summary>
    /// Creates the boundary values of a page of <paramref name="definition"/>.
    /// </summary>
    /// <param name="definition">The pagination query definition that the values belong to.</param>
    /// <param name="values">The values, one per column of <paramref name="definition"/> in column order, each of its column's type.</param>
    /// <returns>The boundary values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> does not hold one value per column.</exception>
    /// <exception cref="InvalidCastException">A value is not of its column's type.</exception>
    /// <exception cref="InvalidOperationException">A value is <see langword="null"/> for a column that does not allow <see langword="null"/>.</exception>
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
        return new PaginationValues<T>(definition, bindings);
    }
#pragma warning restore CA1000

    // Each binding is typed by the column of the definition that created it, so the values are read only by a
    // definition with the same fingerprint: the same column names, types and directions.
    internal ColumnBinding[] GetBindingsFor(PaginationQueryDefinition<T> definition, string paramName)
    {
        if (Definition is null || Definition.SchemaFingerprint != definition.SchemaFingerprint)
            throw new ArgumentException("The values were not created for this pagination definition.", paramName);
        Debug.Assert(Bindings is not null);
        return Bindings;
    }
}
