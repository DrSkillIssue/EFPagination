using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

internal abstract class ProjectionShape<T, TOut> where T : class
{
    public abstract Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source,
        Expression<Func<T, TOut>> selector,
        PaginationColumn<T>[] columns,
        int takeCount,
        PaginationDirection direction,
        CancellationToken ct);
}

internal abstract class ProjectionMaterializedPage<T, TOut> where T : class
{
    public abstract bool HasMore { get; }
    public abstract int Count { get; }
    public abstract List<TOut> ToItemList();
    public abstract void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings);
}

internal static class ProjectionShapeCache<T, TOut> where T : class
{
    private static readonly ConcurrentDictionary<uint, ProjectionShape<T, TOut>> s_cache = new();

    public static ProjectionShape<T, TOut> Get(PaginationQueryDefinition<T> definition)
        => s_cache.GetOrAdd(definition.SchemaFingerprint, static (_, def) => Build(def), definition);

    private static ProjectionShape<T, TOut> Build(PaginationQueryDefinition<T> definition)
    {
        var columns = definition.Columns;
        var arity = columns.Length;
        if (arity is < 1 or > 8)
            throw new NotSupportedException($"Projected pagination supports 1..8 key columns; definition has {arity}.");

        var openShape = arity switch
        {
            1 => typeof(ProjectionShape<,,>),
            2 => typeof(ProjectionShape<,,,>),
            3 => typeof(ProjectionShape<,,,,>),
            4 => typeof(ProjectionShape<,,,,,>),
            5 => typeof(ProjectionShape<,,,,,,>),
            6 => typeof(ProjectionShape<,,,,,,,>),
            7 => typeof(ProjectionShape<,,,,,,,,>),
            _ => typeof(ProjectionShape<,,,,,,,,,>)
        };

        var typeArgs = new Type[arity + 2];
        typeArgs[0] = typeof(T);
        typeArgs[1] = typeof(TOut);
        for (var i = 0; i < arity; i++)
            typeArgs[2 + i] = columns[i].Type;

        var closed = openShape.MakeGenericType(typeArgs);
        return (ProjectionShape<T, TOut>)Activator.CreateInstance(closed)!;
    }
}

internal static class ProjectionShapeHelpers
{
    public static async Task<(List<TEnv> Envelopes, bool HasMore)> MaterializeWrappedAsync<T, TEnv>(
        IQueryable<T> source,
        Expression<Func<T, TEnv>> wrappedLambda,
        int takeCount,
        PaginationDirection direction,
        CancellationToken ct)
    {
        var wrappedQuery = source.Select(wrappedLambda);
        var envelopes = await wrappedQuery.Take(takeCount).ToListAsync(ct).ConfigureAwait(false);
        var hasMore = envelopes.Count == takeCount;
        if (hasMore) envelopes.RemoveAt(envelopes.Count - 1);
        if (direction == PaginationDirection.Backward)
            CollectionsMarshal.AsSpan(envelopes).Reverse();
        return (envelopes, hasMore);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemberInfo Prop(Type t, string name)
        => t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!;
}

internal sealed class ProjectionShape<T, TOut, TK0> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0)])!;
        var members = new[] { ProjectionShapeHelpers.Prop(envType, "Item"), ProjectionShapeHelpers.Prop(envType, "K0") };
        var args = new[] { selector.Body, columns[0].MakeAccessExpression(param) };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0>(
    List<ProjectionEnvelope<TOut, TK0>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1>(
    List<ProjectionEnvelope<TOut, TK0, TK1>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1, TK2> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1, TK2>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1), typeof(TK2)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
            ProjectionShapeHelpers.Prop(envType, "K2"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
            columns[2].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2>(
    List<ProjectionEnvelope<TOut, TK0, TK1, TK2>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
        Unsafe.As<ColumnBinding<TK2>>(bindings[2]).Value = env.K2;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1, TK2, TK3> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1), typeof(TK2), typeof(TK3)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
            ProjectionShapeHelpers.Prop(envType, "K2"),
            ProjectionShapeHelpers.Prop(envType, "K3"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
            columns[2].MakeAccessExpression(param),
            columns[3].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3>(
    List<ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
        Unsafe.As<ColumnBinding<TK2>>(bindings[2]).Value = env.K2;
        Unsafe.As<ColumnBinding<TK3>>(bindings[3]).Value = env.K3;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1, TK2, TK3, TK4> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1), typeof(TK2), typeof(TK3), typeof(TK4)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
            ProjectionShapeHelpers.Prop(envType, "K2"),
            ProjectionShapeHelpers.Prop(envType, "K3"),
            ProjectionShapeHelpers.Prop(envType, "K4"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
            columns[2].MakeAccessExpression(param),
            columns[3].MakeAccessExpression(param),
            columns[4].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4>(
    List<ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
        Unsafe.As<ColumnBinding<TK2>>(bindings[2]).Value = env.K2;
        Unsafe.As<ColumnBinding<TK3>>(bindings[3]).Value = env.K3;
        Unsafe.As<ColumnBinding<TK4>>(bindings[4]).Value = env.K4;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1), typeof(TK2), typeof(TK3), typeof(TK4), typeof(TK5)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
            ProjectionShapeHelpers.Prop(envType, "K2"),
            ProjectionShapeHelpers.Prop(envType, "K3"),
            ProjectionShapeHelpers.Prop(envType, "K4"),
            ProjectionShapeHelpers.Prop(envType, "K5"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
            columns[2].MakeAccessExpression(param),
            columns[3].MakeAccessExpression(param),
            columns[4].MakeAccessExpression(param),
            columns[5].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5>(
    List<ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
        Unsafe.As<ColumnBinding<TK2>>(bindings[2]).Value = env.K2;
        Unsafe.As<ColumnBinding<TK3>>(bindings[3]).Value = env.K3;
        Unsafe.As<ColumnBinding<TK4>>(bindings[4]).Value = env.K4;
        Unsafe.As<ColumnBinding<TK5>>(bindings[5]).Value = env.K5;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1), typeof(TK2), typeof(TK3), typeof(TK4), typeof(TK5), typeof(TK6)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
            ProjectionShapeHelpers.Prop(envType, "K2"),
            ProjectionShapeHelpers.Prop(envType, "K3"),
            ProjectionShapeHelpers.Prop(envType, "K4"),
            ProjectionShapeHelpers.Prop(envType, "K5"),
            ProjectionShapeHelpers.Prop(envType, "K6"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
            columns[2].MakeAccessExpression(param),
            columns[3].MakeAccessExpression(param),
            columns[4].MakeAccessExpression(param),
            columns[5].MakeAccessExpression(param),
            columns[6].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>(
    List<ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
        Unsafe.As<ColumnBinding<TK2>>(bindings[2]).Value = env.K2;
        Unsafe.As<ColumnBinding<TK3>>(bindings[3]).Value = env.K3;
        Unsafe.As<ColumnBinding<TK4>>(bindings[4]).Value = env.K4;
        Unsafe.As<ColumnBinding<TK5>>(bindings[5]).Value = env.K5;
        Unsafe.As<ColumnBinding<TK6>>(bindings[6]).Value = env.K6;
    }
}

internal sealed class ProjectionShape<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7> : ProjectionShape<T, TOut> where T : class
{
    public override async Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source, Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns,
        int takeCount, PaginationDirection direction, CancellationToken ct)
    {
        var lambda = BuildLambda(selector, columns);
        var (envelopes, hasMore) = await ProjectionShapeHelpers
            .MaterializeWrappedAsync(source, lambda, takeCount, direction, ct).ConfigureAwait(false);
        return new ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>(envelopes, hasMore);
    }

    private static Expression<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>>> BuildLambda(
        Expression<Func<T, TOut>> selector, PaginationColumn<T>[] columns)
    {
        var param = selector.Parameters[0];
        var envType = typeof(ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>);
        var ctor = envType.GetConstructor([typeof(TOut), typeof(TK0), typeof(TK1), typeof(TK2), typeof(TK3), typeof(TK4), typeof(TK5), typeof(TK6), typeof(TK7)])!;
        var members = new[]
        {
            ProjectionShapeHelpers.Prop(envType, "Item"),
            ProjectionShapeHelpers.Prop(envType, "K0"),
            ProjectionShapeHelpers.Prop(envType, "K1"),
            ProjectionShapeHelpers.Prop(envType, "K2"),
            ProjectionShapeHelpers.Prop(envType, "K3"),
            ProjectionShapeHelpers.Prop(envType, "K4"),
            ProjectionShapeHelpers.Prop(envType, "K5"),
            ProjectionShapeHelpers.Prop(envType, "K6"),
            ProjectionShapeHelpers.Prop(envType, "K7"),
        };
        var args = new[]
        {
            selector.Body,
            columns[0].MakeAccessExpression(param),
            columns[1].MakeAccessExpression(param),
            columns[2].MakeAccessExpression(param),
            columns[3].MakeAccessExpression(param),
            columns[4].MakeAccessExpression(param),
            columns[5].MakeAccessExpression(param),
            columns[6].MakeAccessExpression(param),
            columns[7].MakeAccessExpression(param),
        };
        return Expression.Lambda<Func<T, ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>>>(Expression.New(ctor, args, members), param);
    }
}

internal sealed class ProjectionMaterializedPage<T, TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>(
    List<ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>> envelopes, bool hasMore) : ProjectionMaterializedPage<T, TOut> where T : class
{
    public override bool HasMore { get; } = hasMore;
    public override int Count => envelopes.Count;

    public override List<TOut> ToItemList()
    {
        var span = CollectionsMarshal.AsSpan(envelopes);
        var list = new List<TOut>(span.Length);
        for (var i = 0; i < span.Length; i++) list.Add(span[i].Item);
        return list;
    }

    public override void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings)
    {
        ref var env = ref CollectionsMarshal.AsSpan(envelopes)[itemIndex];
        Unsafe.As<ColumnBinding<TK0>>(bindings[0]).Value = env.K0;
        Unsafe.As<ColumnBinding<TK1>>(bindings[1]).Value = env.K1;
        Unsafe.As<ColumnBinding<TK2>>(bindings[2]).Value = env.K2;
        Unsafe.As<ColumnBinding<TK3>>(bindings[3]).Value = env.K3;
        Unsafe.As<ColumnBinding<TK4>>(bindings[4]).Value = env.K4;
        Unsafe.As<ColumnBinding<TK5>>(bindings[5]).Value = env.K5;
        Unsafe.As<ColumnBinding<TK6>>(bindings[6]).Value = env.K6;
        Unsafe.As<ColumnBinding<TK7>>(bindings[7]).Value = env.K7;
    }
}
