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
    private readonly List<PaginationColumn<T>> _columns = [];

    internal PaginationColumn<T>[] ColumnsArray
    {
        get => field ??= [.. _columns];
    }

    /// <summary>Adds an ascending column to the pagination definition.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="columnExpression"/> is <see langword="null"/>.</exception>
    public PaginationBuilder<T> Ascending<TColumn>(Expression<Func<T, TColumn>> columnExpression)
        => ConfigureColumn(columnExpression, isDescending: false);

    /// <summary>Adds a descending column to the pagination definition.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="columnExpression"/> is <see langword="null"/>.</exception>
    public PaginationBuilder<T> Descending<TColumn>(Expression<Func<T, TColumn>> columnExpression)
        => ConfigureColumn(columnExpression, isDescending: true);

    /// <summary>Adds a column with an explicit sort direction.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="columnExpression"/> is <see langword="null"/>.</exception>
    public PaginationBuilder<T> ConfigureColumn<TColumn>(
        Expression<Func<T, TColumn>> columnExpression,
        bool isDescending)
    {
        ArgumentNullException.ThrowIfNull(columnExpression);
        MaybeRegisterEnum(typeof(TColumn));
        _columns.Add(new PaginationColumn<T, TColumn>(isDescending, columnExpression));
        return this;
    }

    internal PaginationBuilder<T> Column(string propertyName, bool isDescending)
    {
        var pi = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new ArgumentException($"Public instance property '{propertyName}' not found on type '{typeof(T).Name}'.", nameof(propertyName));

        MaybeRegisterEnum(pi.PropertyType);

        var param = Expression.Parameter(typeof(T), "x");
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(T), pi.PropertyType);
        var lambda = Expression.Lambda(delegateType, Expression.Property(param, pi), param);

        var concreteType = typeof(PaginationColumn<,>).MakeGenericType(typeof(T), pi.PropertyType);
        _columns.Add((PaginationColumn<T>)Activator.CreateInstance(concreteType, isDescending, lambda)!);
        return this;
    }

    private static void MaybeRegisterEnum(Type columnType)
    {
        var underlying = Nullable.GetUnderlyingType(columnType) ?? columnType;
        if (underlying.IsEnum)
            PaginationCursor.RegisterEnumType(underlying);
    }
}
