using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Fluent builder for defining the columns that make up a pagination definition.
/// Columns are added in order of significance (most significant first).
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PaginationBuilder<T>
{
    private static readonly ConcurrentDictionary<Type, Func<bool, LambdaExpression, PaginationColumn<T>>> s_columnFactories = new();

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

    internal PaginationBuilder<T> Column(string propertyName, bool isDescending)
    {
        var pi = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new ArgumentException($"Public instance property '{propertyName}' not found on type '{typeof(T).Name}'.", nameof(propertyName));

        var param = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(param, pi);
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(T), pi.PropertyType);
        var lambda = Expression.Lambda(delegateType, property, param);

        var factory = GetOrCreateColumnFactory(pi.PropertyType);
        _columns.Add(factory(isDescending, lambda));
        return this;
    }

    private static Func<bool, LambdaExpression, PaginationColumn<T>> GetOrCreateColumnFactory(Type columnType)
    {
        return s_columnFactories.GetOrAdd(columnType, static ct =>
        {
            var paginationColumnType = typeof(PaginationColumn<,>).MakeGenericType(typeof(T), ct);
            var funcType = typeof(Func<,>).MakeGenericType(typeof(T), ct);
            var exprType = typeof(Expression<>).MakeGenericType(funcType);

            var ctor = paginationColumnType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                [typeof(bool), exprType])!;

            var descParam = Expression.Parameter(typeof(bool), "desc");
            var exprParam = Expression.Parameter(typeof(LambdaExpression), "expr");
            var typedExpr = Expression.Convert(exprParam, exprType);

            var newExpr = Expression.New(ctor, descParam, typedExpr);
            var castExpr = Expression.Convert(newExpr, typeof(PaginationColumn<T>));

            return Expression.Lambda<Func<bool, LambdaExpression, PaginationColumn<T>>>(
                castExpr, descParam, exprParam).Compile();
        });
    }
}
