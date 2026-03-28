using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

internal static class QueryableMethods
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodInfo Get(string name, int parameterCount)
    {
        return typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == name && m.GetParameters().Length == parameterCount);
    }

    internal static class Where<T>
    {
        internal static readonly MethodInfo Method = typeof(Queryable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Queryable.Where)
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(T));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IQueryable<T> ApplyWhere<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate)
    {
        var call = FastExpressions.Call(Where<T>.Method, source.Expression, FastExpressions.Quote(predicate));
        return source.Provider.CreateQuery<T>(call);
    }
}
