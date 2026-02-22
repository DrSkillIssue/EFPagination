using System.Linq.Expressions;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Fluent builder for defining the columns that make up a pagination definition.
/// Columns are added in order of significance (most significant first).
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PaginationBuilder<T>
{
    private readonly List<PaginationColumn<T>> _columns = [];

    /// <summary>
    /// Returns the configured columns as an array. Called once after the builder action completes.
    /// </summary>
    internal PaginationColumn<T>[] ColumnsArray => [.. _columns];

    /// <summary>
    /// Adds an ascending column to the pagination definition.
    /// </summary>
    /// <typeparam name="TColumn">The column value type.</typeparam>
    /// <param name="columnExpression">A lambda selecting the column property from the entity.</param>
    /// <returns>This builder for chaining.</returns>
    public PaginationBuilder<T> Ascending<TColumn>(
        Expression<Func<T, TColumn>> columnExpression) => ConfigureColumn(columnExpression, isDescending: false);

    /// <summary>
    /// Adds a descending column to the pagination definition.
    /// </summary>
    /// <typeparam name="TColumn">The column value type.</typeparam>
    /// <param name="columnExpression">A lambda selecting the column property from the entity.</param>
    /// <returns>This builder for chaining.</returns>
    public PaginationBuilder<T> Descending<TColumn>(
        Expression<Func<T, TColumn>> columnExpression) => ConfigureColumn(columnExpression, isDescending: true);

    /// <summary>
    /// Adds a column to the pagination definition with an explicit sort direction.
    /// </summary>
    /// <typeparam name="TColumn">The column value type.</typeparam>
    /// <param name="columnExpression">A lambda selecting the column property from the entity.</param>
    /// <param name="isDescending">If <see langword="true"/>, the column is sorted descending; otherwise ascending.</param>
    /// <returns>This builder for chaining.</returns>
    public PaginationBuilder<T> ConfigureColumn<TColumn>(
        Expression<Func<T, TColumn>> columnExpression,
        bool isDescending)
    {
        _columns.Add(new PaginationColumn<T, TColumn>(isDescending, columnExpression));
        return this;
    }
}
