using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Provides helper methods for navigating and extracting information from <see cref="MemberExpression"/> chains.
/// </summary>
internal static class ExpressionHelper
{
    /// <summary>
    /// Walks a <see cref="MemberExpression"/> chain to find the root expression.
    /// For <c>x.Prop1.Prop2</c>, returns the <c>x</c> parameter expression.
    /// </summary>
    /// <param name="expression">The member expression to walk.</param>
    /// <returns>The root expression at the base of the member access chain.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Expression GetStartingExpression(
        MemberExpression expression)
    {
        ValidateExpressionUnwrapped(expression);

        var current = (Expression)expression;
        while (current is MemberExpression memberExpression)
        {
            current = memberExpression.Expression;
        }

        return current!;
    }

    /// <summary>
    /// Extracts the chain of <see cref="PropertyInfo"/> members from a <see cref="MemberExpression"/>.
    /// For <c>x.Prop1.Prop2</c>, returns <c>[Prop1, Prop2]</c> in forward order.
    /// </summary>
    /// <param name="expression">The member expression to decompose.</param>
    /// <returns>An array of property infos in forward (root-to-leaf) order.</returns>
    /// <exception cref="InvalidOperationException">If the chain contains a non-property member access.</exception>
    public static PropertyInfo[] GetPropertyChain(
        MemberExpression expression)
    {
        ValidateExpressionUnwrapped(expression);

        var depth = 0;
        var current = (Expression)expression;
        while (current is MemberExpression expression1)
        {
            depth++;
            current = expression1.Expression;
        }

        var result = new PropertyInfo[depth];
        current = expression;
        var index = depth - 1;
        while (current is MemberExpression memberExpression)
        {
            result[index--] = GetPropertyInfoMember(memberExpression);
            current = memberExpression.Expression;
        }

        return result;
    }

    [Conditional("DEBUG")]
    private static void ValidateExpressionUnwrapped(Expression expression)
    {
        if (expression.NodeType is ExpressionType.Lambda or ExpressionType.Convert)
        {
            throw new UnreachableException("Expression should have been unwrapped by now.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyInfo GetPropertyInfoMember(MemberExpression memberExpression)
    {
        if (memberExpression.Member is PropertyInfo prop)
        {
            return prop;
        }

        throw new InvalidOperationException($"Expected a property access, got '{memberExpression.Member.MemberType}'.");
    }
}
