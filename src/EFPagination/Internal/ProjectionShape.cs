using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;

namespace EFPagination.Internal;

/// <summary>
/// An arity-specialized projection materializer. Concrete subclasses are closed-generic over the
/// projected type <typeparamref name="TOut"/> and the keyset key column types, so EF Core sees a
/// fully-typed <see cref="Expression.New(ConstructorInfo, Expression[])"/>
/// projection and emits a single covering <c>SELECT</c>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
internal abstract class ProjectionShape<T, TOut> where T : class
{
    /// <summary>
    /// Wraps <paramref name="selector"/> in a typed envelope and materializes <paramref name="takeCount"/>
    /// rows from <paramref name="source"/>, in correct presentation order.
    /// </summary>
    /// <param name="source">The ordered, optionally filtered query.</param>
    /// <param name="selector">The user-supplied projection.</param>
    /// <param name="columns">The pagination columns whose values must accompany each row.</param>
    /// <param name="takeCount">The number of envelopes to materialize (typically <c>pageSize + 1</c>).</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A materialized page exposing the projected items and a typed key extractor.</returns>
    public abstract Task<ProjectionMaterializedPage<T, TOut>> MaterializeAsync(
        IQueryable<T> source,
        Expression<Func<T, TOut>> selector,
        PaginationColumn<T>[] columns,
        int takeCount,
        PaginationDirection direction,
        CancellationToken ct);
}

/// <summary>
/// Holds the result of a <see cref="ProjectionShape{T, TOut}.MaterializeAsync"/> call and exposes
/// boxing-free extraction of the keyset key values for a given row index.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
internal abstract class ProjectionMaterializedPage<T, TOut> where T : class
{
    /// <summary>
    /// Gets a value indicating whether the source query had more rows past the trailing envelope.
    /// </summary>
    public abstract bool HasMore { get; }

    /// <summary>
    /// Gets the number of envelopes in the page.
    /// </summary>
    public abstract int Count { get; }

    /// <summary>
    /// Materializes the projected items into a <see cref="List{TOut}"/>.
    /// </summary>
    /// <returns>The projected items.</returns>
    public abstract List<TOut> ToItemList();

    /// <summary>
    /// Writes the keyset key values for the envelope at <paramref name="itemIndex"/> into the
    /// supplied bindings, without boxing.
    /// </summary>
    /// <param name="itemIndex">The zero-based envelope index.</param>
    /// <param name="bindings">The typed destination bindings, one per definition column.</param>
    public abstract void ExtractKeysIntoBindings(int itemIndex, ColumnBinding[] bindings);
}

/// <summary>
/// Per-definition cache of arity-closed <see cref="ProjectionShape{T, TOut}"/> instances. The
/// closed generic shape is built once per <see cref="PaginationQueryDefinition{T}.SchemaFingerprint"/>
/// and reused across calls.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
internal static class ProjectionShapeCache<T, TOut> where T : class
{
    private static readonly ConcurrentDictionary<uint, ProjectionShape<T, TOut>> s_cache = new();

    /// <summary>
    /// Returns the cached <see cref="ProjectionShape{T, TOut}"/> for <paramref name="definition"/>,
    /// building one on first use.
    /// </summary>
    /// <param name="definition">The pagination definition driving the shape arity and key types.</param>
    /// <returns>The matching <see cref="ProjectionShape{T, TOut}"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="definition"/> has fewer than 1 or more than 8 key columns.</exception>
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

/// <summary>
/// Shared helpers for the arity-specialized projection shapes: the EF Core materialization step
/// (with overflow trim and direction-aware reversal) and a member-info lookup helper for the
/// envelope construction lambda.
/// </summary>
internal static class ProjectionShapeHelpers
{
    /// <summary>
    /// Wraps <paramref name="source"/> in the supplied projection lambda, materializes
    /// <paramref name="takeCount"/> envelopes, trims the overflow row, and reverses in-place
    /// when paginating backward.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TEnv">The closed envelope type.</typeparam>
    /// <param name="source">The ordered, optionally filtered source query.</param>
    /// <param name="wrappedLambda">The wrapped projection that constructs envelopes.</param>
    /// <param name="takeCount">The number of envelopes to fetch (<c>pageSize + 1</c>).</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The materialized envelopes and a <c>HasMore</c> flag.</returns>
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

    /// <summary>
    /// Returns the <see cref="PropertyInfo"/> for a public instance property by name. Cached
    /// reflection helpers in this codebase already exist; this overload is used only during
    /// shape lambda construction.
    /// </summary>
    /// <param name="t">The type to search.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The matched <see cref="PropertyInfo"/>.</returns>
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
