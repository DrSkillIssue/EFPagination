using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Cached <see cref="MethodInfo"/> lookups for the <see cref="Queryable"/> methods we apply via
/// reflective <see cref="Expression.Call(MethodInfo, Expression[])"/> invocations.
/// </summary>
internal static class QueryableMethods
{
    /// <summary>
    /// Returns the public-static <see cref="Queryable"/> method matching the supplied name and arity.
    /// </summary>
    /// <param name="name">The method name (for example, <c>OrderBy</c>).</param>
    /// <param name="parameterCount">The number of parameters to match.</param>
    /// <returns>The first matching <see cref="MethodInfo"/>.</returns>
    /// <exception cref="InvalidOperationException">No method on <see cref="Queryable"/> matches.</exception>
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

    /// <summary>
    /// Applies <see cref="Queryable.Where{TSource}(IQueryable{TSource}, Expression{Func{TSource, bool}})"/>
    /// to <paramref name="source"/> via the cached <see cref="MethodInfo"/>, avoiding the
    /// reflection cost of resolving the overload on each call.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>The filtered query.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IQueryable<T> ApplyWhere<T>(IQueryable<T> source, Expression<Func<T, bool>> predicate)
    {
        var call = Expression.Call(Where<T>.Method, source.Expression, Expression.Quote(predicate));
        return source.Provider.CreateQuery<T>(call);
    }
}
