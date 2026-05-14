using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace EFPagination.Internal;

/// <summary>
/// Provides static methods for adapting lambda expression parameters and types.
/// Used to rebind column access expressions to different entity parameters or reference types.
/// </summary>
internal static class AdaptingExpressionVisitor
{
    /// <summary>
    /// Rebinds a lambda's parameter to <paramref name="newParameter"/>.
    /// </summary>
    public static Expression<Func<T, TColumn>> AdaptParameter<T, TColumn>(
        Expression<Func<T, TColumn>> expression,
        ParameterExpression newParameter)
    {
        Debug.Assert(expression.Parameters.Count == 1);

        var visitor = new ParameterAdaptingExpressionVisitor<T, TColumn>(
            expression.Parameters[0], newParameter);
        var newBody = visitor.Visit(expression.Body);
        return Expression.Lambda<Func<T, TColumn>>(newBody, [newParameter]);
    }

    /// <summary>
    /// Adapts a lambda to accept an <see cref="object"/> parameter and access equivalent
    /// properties on <paramref name="newType"/> via loose-typing rules.
    /// </summary>
    public static Expression<Func<object, TColumn>> AdaptType<T, TColumn>(
        Expression<Func<T, TColumn>> expression,
        Type newType)
    {
        Debug.Assert(expression.Parameters.Count == 1);

        var newParameter = Expression.Parameter(typeof(object), expression.Parameters[0].Name);
        var visitor = new TypeAdaptingExpressionVisitor<T, TColumn>(
            expression.Parameters[0], newParameter, newType);
        var newBody = visitor.Visit(expression.Body);
        return Expression.Lambda<Func<object, TColumn>>(newBody, [newParameter]);
    }
}

internal class ParameterAdaptingExpressionVisitor<T, TColumn>(
    ParameterExpression oldParameter,
    ParameterExpression newParameter) : ExpressionVisitor
{
    protected ParameterExpression OldParameter { get; } = oldParameter;

    protected override Expression VisitParameter(ParameterExpression node)
        => node == OldParameter ? newParameter : node;
}

internal sealed class TypeAdaptingExpressionVisitor<T, TColumn>(
    ParameterExpression oldParameter,
    ParameterExpression newParameter,
    Type? newType) : ParameterAdaptingExpressionVisitor<T, TColumn>(oldParameter, newParameter)
{
    protected override Expression VisitMember(MemberExpression node)
    {
        if (newType is null)
            return base.VisitMember(node);

        // Walk the chain to its root. Skip remapping when the root isn't the original parameter
        // (e.g. static field/property like DateTime.MinValue terminates at a null Expression).
        var depth = 0;
        Expression? cursor = node;
        while (cursor is MemberExpression cm)
        {
            depth++;
            cursor = cm.Expression;
        }

        if (cursor != OldParameter)
            return base.VisitMember(node);

        // Collect properties in root-to-leaf order. Only property accesses are valid past this point.
        var properties = new PropertyInfo[depth];
        cursor = node;
        var idx = depth - 1;
        while (cursor is MemberExpression cm)
        {
            properties[idx--] = cm.Member as PropertyInfo
                ?? throw new InvalidOperationException("Pagination column member-access chain must contain only properties.");
            cursor = cm.Expression;
        }

        // Build the rebound member-access chain on the new parameter type.
        var replacement = (Expression)Expression.Convert(Visit(OldParameter), newType);
        foreach (var property in properties)
        {
            if (!AccessorCache.TryGetProperty(replacement.Type, property.Name, out var newProperty))
                ThrowIncompatibleObject(property.Name, replacement.Type);
            replacement = Expression.MakeMemberAccess(replacement, newProperty);
        }

        return replacement;
    }

    [DoesNotReturn]
    private static void ThrowIncompatibleObject(string propertyName, Type referenceType) =>
        throw new IncompatibleReferenceException(
            $"Projection type '{referenceType.Name}' is missing property '{propertyName}' " +
            $"required by the pagination definition on '{typeof(T).Name}'. " +
            $"Ensure your projected DTO includes all pagination column properties.",
            propertyName, referenceType, typeof(T));
}
