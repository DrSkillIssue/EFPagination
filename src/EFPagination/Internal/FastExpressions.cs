using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

internal static class FastExpressions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodCallExpression Call(MethodInfo method, Expression arg0, Expression arg1)
        => Expression.Call(method, arg0, arg1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnaryExpression Quote(LambdaExpression lambda)
        => Expression.Quote(lambda);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnaryExpression Convert(Expression operand, Type type)
        => Expression.Convert(operand, type);
}
