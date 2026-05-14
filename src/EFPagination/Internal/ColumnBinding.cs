using System.Linq.Expressions;
using System.Reflection;

namespace EFPagination.Internal;

/// <summary>
/// Typed slot that carries a single pagination boundary value through to the EF Core predicate
/// pipeline. The filter strategy substitutes the per-column placeholder with
/// <see cref="CreateValueAccessExpression"/>; EF Core then sees a <see cref="MemberExpression"/>
/// on a <see cref="ConstantExpression"/> and parameterizes the value into SQL. No boxing for
/// value types, no <see cref="ExpressionType.Convert"/> nodes.
/// </summary>
internal abstract class ColumnBinding
{
    /// <summary>
    /// Builds an expression that reads this binding's value, typed to the column's CLR type.
    /// </summary>
    /// <returns>A <see cref="MemberExpression"/> over a constant reference to this binding.</returns>
    public abstract Expression CreateValueAccessExpression();

    /// <summary>
    /// Returns the current bound value as a boxed object. Used only by diagnostics paths.
    /// </summary>
    /// <returns>The boxed bound value, or <see langword="null"/>.</returns>
    public abstract object? GetValueBoxed();
}

/// <summary>
/// Strongly-typed <see cref="ColumnBinding"/> backed by a writable instance field.
/// </summary>
/// <typeparam name="TKey">The column CLR type.</typeparam>
internal sealed class ColumnBinding<TKey> : ColumnBinding
{
    private static readonly FieldInfo s_valueField =
        typeof(ColumnBinding<TKey>).GetField(nameof(Value))!;

    /// <summary>
    /// The current boundary value. Mutated directly by writers; read by EF Core during query
    /// translation via <see cref="CreateValueAccessExpression"/>.
    /// </summary>
    public TKey Value = default!;

    /// <inheritdoc/>
    public override Expression CreateValueAccessExpression()
        => Expression.Field(Expression.Constant(this), s_valueField);

    /// <inheritdoc/>
    public override object? GetValueBoxed() => Value;
}
