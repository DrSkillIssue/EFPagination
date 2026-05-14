using System.Linq.Expressions;
using System.Reflection;

namespace EFPagination.Internal;

/// <summary>
/// Typed slot that carries a single pagination boundary value through to the EF Core predicate
/// pipeline. The strategy substitutes the per-column placeholder with
/// <see cref="CreateValueAccessExpression"/>; EF Core sees a <see cref="MemberExpression"/> on a
/// <see cref="ConstantExpression"/> and parameterizes the value into SQL. No boxing for value
/// types, no <c>Convert</c> expression nodes.
/// </summary>
internal abstract class ColumnBinding
{
    /// <summary>
    /// Builds an expression that reads this binding's value, typed to the column's CLR type.
    /// </summary>
    public abstract Expression CreateValueAccessExpression();

    /// <summary>
    /// Returns the current bound value as a boxed object. Used only by diagnostics paths.
    /// </summary>
    public abstract object? GetValueBoxed();
}

internal sealed class ColumnBinding<TKey> : ColumnBinding
{
    private static readonly FieldInfo s_valueField =
        typeof(ColumnBinding<TKey>).GetField(nameof(Value))!;

    public TKey Value = default!;

    public override Expression CreateValueAccessExpression()
        => Expression.Field(Expression.Constant(this), s_valueField);

    public override object? GetValueBoxed() => Value;
}
