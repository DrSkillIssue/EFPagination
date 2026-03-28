using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace EFPagination.Internal;

/// <summary>
/// Provides static methods for adapting lambda expression parameters and types.
/// Used to rebind column access expressions to different entity parameters or reference types.
/// </summary>
internal static class AdaptingExpressionVisitor
{
    /// <summary>
    /// Rebinds a lambda's parameter to <paramref name="newParameter"/>, returning a new lambda
    /// whose body accesses the new parameter instead of the original.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TColumn">The column value type.</typeparam>
    /// <param name="expression">The original lambda expression.</param>
    /// <param name="newParameter">The new parameter to substitute in.</param>
    /// <returns>A new lambda expression bound to <paramref name="newParameter"/>.</returns>
    public static Expression<Func<T, TColumn>> AdaptParameter<T, TColumn>(
        Expression<Func<T, TColumn>> expression,
        ParameterExpression newParameter)
    {
        Debug.Assert(expression.Parameters.Count == 1);

        var visitor = new ParameterAdaptingExpressionVisitor<T, TColumn>(
            expression.Parameters[0],
            newParameter);
        var newBody = visitor.Visit(expression.Body);
        return Expression.Lambda<Func<T, TColumn>>(newBody, [newParameter]);
    }

    /// <summary>
    /// Adapts a lambda to accept an <see cref="object"/> parameter and access equivalent properties
    /// on <paramref name="newType"/> via loose typing rules. The resulting lambda casts the input
    /// to the target type and maps the property chain accordingly.
    /// </summary>
    /// <typeparam name="T">The original entity type.</typeparam>
    /// <typeparam name="TColumn">The column value type.</typeparam>
    /// <param name="expression">The original lambda expression.</param>
    /// <param name="newType">The actual runtime type of the reference object.</param>
    /// <returns>A new lambda accepting <see cref="object"/> and accessing the mapped properties.</returns>
    public static Expression<Func<object, TColumn>> AdaptType<T, TColumn>(
        Expression<Func<T, TColumn>> expression,
        Type newType)
    {
        Debug.Assert(expression.Parameters.Count == 1);

        var newParameter = Expression.Parameter(typeof(object), expression.Parameters[0].Name);
        var visitor = new TypeAdaptingExpressionVisitor<T, TColumn>(
            expression.Parameters[0],
            newParameter,
            newType);
        var newBody = visitor.Visit(expression.Body);
        return Expression.Lambda<Func<object, TColumn>>(newBody, [newParameter]);
    }
}

/// <summary>
/// Replaces occurrences of <paramref name="oldParameter"/> with <paramref name="newParameter"/>
/// in an expression tree.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TColumn">The column value type.</typeparam>
internal class ParameterAdaptingExpressionVisitor<T, TColumn>(
    ParameterExpression oldParameter,
    ParameterExpression newParameter) : ExpressionVisitor
{
    /// <summary>
    /// The original parameter being replaced.
    /// </summary>
    protected ParameterExpression OldParameter { get; } = oldParameter;

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node) => node == OldParameter ? newParameter : node;
}

/// <summary>
/// Extends <see cref="ParameterAdaptingExpressionVisitor{T, TColumn}"/> to also remap
/// member access chains to equivalent properties on a different type. Supports loose typing
/// where the reference object is not the same type as the entity.
/// </summary>
/// <typeparam name="T">The original entity type.</typeparam>
/// <typeparam name="TColumn">The column value type.</typeparam>
internal sealed class TypeAdaptingExpressionVisitor<T, TColumn>(
    ParameterExpression oldParameter,
    ParameterExpression newParameter,
    Type? newType) : ParameterAdaptingExpressionVisitor<T, TColumn>(oldParameter, newParameter)
{
    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        if (newType is null)
        {
            return base.VisitMember(node);
        }

        var startingExpression = ExpressionHelper.GetStartingExpression(node);
        if (startingExpression != OldParameter)
        {
            return base.VisitMember(node);
        }

        var currentReplacementExpression = (Expression)Expression.Convert(Visit(startingExpression), newType);
        var properties = ExpressionHelper.GetPropertyChain(node);

        foreach (var property in properties)
        {
            var accessor = Accessor.Obtain(currentReplacementExpression.Type);
            if (!accessor.TryGetProperty(property.Name, out var newProperty))
            {
                ThrowIncompatibleObject(property.Name, currentReplacementExpression.Type);
            }
            currentReplacementExpression = Expression.MakeMemberAccess(currentReplacementExpression, newProperty);
        }

        return currentReplacementExpression;
    }

    [DoesNotReturn]
    private static void ThrowIncompatibleObject(string propertyName, Type referenceType) =>
        throw new IncompatibleReferenceException(
            $"Projection type '{referenceType.Name}' is missing property '{propertyName}' " +
            $"required by the pagination definition on '{typeof(T).Name}'. " +
            $"Ensure your projected DTO includes all pagination column properties.",
            propertyName, referenceType, typeof(T));
}
