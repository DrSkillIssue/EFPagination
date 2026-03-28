using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace EFPagination.Internal;

/// <summary>
/// Caches the structural shape of a pagination filter predicate expression tree per direction,
/// enabling per-call instantiation by substituting placeholder nodes with actual reference values.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
/// <remarks>
/// Initializes a new template cache for the specified columns and strategy.
/// </remarks>
/// <param name="columns">The pagination columns defining the sort order.</param>
/// <param name="strategy">The strategy that builds the predicate expression tree structure.</param>
internal sealed class CachedPredicateTemplate<T>(
    PaginationColumn<T>[] columns,
    IFilterPredicateStrategyInternal strategy)
{
    private volatile TemplateInstance? _forwardTemplate;
    private volatile TemplateInstance? _backwardTemplate;
    private readonly PaginationColumn<T>[] _columns = columns;
    private readonly IFilterPredicateStrategyInternal _strategy = strategy;

    /// <summary>
    /// Builds a pagination filter predicate by substituting the cached template's placeholder nodes
    /// with the given reference values.
    /// </summary>
    /// <param name="direction">The pagination direction (<see cref="PaginationDirection.Forward"/> or <see cref="PaginationDirection.Backward"/>).</param>
    /// <param name="referenceValues">The column values extracted from the reference object, one per pagination column.</param>
    /// <returns>A lambda expression suitable for use in a LINQ <c>Where</c> clause.</returns>
    public Expression<Func<T, bool>> Build(
        PaginationDirection direction,
        object?[] referenceValues)
    {
        var template = GetTemplate(direction);
        return template.Instantiate(referenceValues);
    }

    public Expression<Func<T, bool>> Build<TReference>(
        PaginationDirection direction,
        PaginationColumn<T>[] columns,
        TReference reference)
    {
        var template = GetTemplate(direction);
        return template.Instantiate(columns, reference);
    }

    internal TemplateInstance ForwardTemplate => _forwardTemplate ??= BuildTemplate(PaginationDirection.Forward);

    internal TemplateInstance BackwardTemplate => _backwardTemplate ??= BuildTemplate(PaginationDirection.Backward);

    private static readonly string[] s_placeholderNames =
    [
        "__ph_0", "__ph_1", "__ph_2", "__ph_3",
        "__ph_4", "__ph_5", "__ph_6", "__ph_7",
    ];

    /// <summary>
    /// Builds the one-time template for a given direction by constructing the full expression tree
    /// with placeholder <see cref="ParameterExpression"/> nodes in place of actual values.
    /// </summary>
    private TemplateInstance BuildTemplate(PaginationDirection direction)
    {
        var count = _columns.Length;

        var placeholders = new ParameterExpression[count];
        var placeholderExpressions = new Expression[count];
        for (var i = 0; i < count; i++)
        {
            var name = i < s_placeholderNames.Length ? s_placeholderNames[i] : $"__ph_{i}";
            placeholders[i] = Expression.Parameter(typeof(object), name);
            placeholderExpressions[i] = placeholders[i];
        }

        var entityParam = Expression.Parameter(typeof(T), "entity");
        var templateBody = _strategy.BuildExpressionCoreForTemplate(
            _columns, direction, placeholderExpressions, entityParam);

        return new TemplateInstance(templateBody, entityParam, placeholders, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TemplateInstance GetTemplate(PaginationDirection direction)
    {
        return direction == PaginationDirection.Forward
            ? (_forwardTemplate ??= BuildTemplate(direction))
            : (_backwardTemplate ??= BuildTemplate(direction));
    }

    /// <summary>
    /// Holds the pre-built template expression tree. Each call to <c>Instantiate</c> allocates
    /// its own <see cref="ValueHolder"/> instances and substitutes them into the template,
    /// producing a thread-safe per-call lambda expression.
    /// </summary>
    internal sealed class TemplateInstance(
        Expression templateBody,
        ParameterExpression entityParam,
        ParameterExpression[] placeholders,
        int columnCount)
    {
        private readonly Expression _templateBody = templateBody;
        private readonly ParameterExpression _entityParam = entityParam;
        private readonly ParameterExpression[] _placeholders = placeholders;
        private readonly int _columnCount = columnCount;

        private readonly SpineReconstructor? _spineReconstructor = SpineReconstructor.TryCreate(templateBody, placeholders);

        public Expression<Func<T, bool>> Instantiate(object?[] referenceValues)
        {
            var replacements = RentReplacements();
            for (var i = 0; i < _columnCount; i++)
            {
                var holder = new ValueHolder { Value = referenceValues[i] };
                replacements[i] = Expression.Field(
                    Expression.Constant(holder), ValueHolder.ValueField);
            }
            return BuildLambda(replacements);
        }

        public Expression<Func<T, bool>> Instantiate<TReference>(PaginationColumn<T>[] columns, TReference reference)
        {
            var replacements = RentReplacements();
            for (var i = 0; i < _columnCount; i++)
            {
                var holder = new ValueHolder { Value = columns[i].ObtainValue(reference) };
                replacements[i] = Expression.Field(
                    Expression.Constant(holder), ValueHolder.ValueField);
            }
            return BuildLambda(replacements);
        }

        public Expression<Func<T, bool>> InstantiateFromColumnValues(
            PaginationColumn<T>[] columns,
            ReadOnlySpan<ColumnValue> values)
        {
            var replacements = RentReplacements();

            if (!TryPopulatePositional(columns, values, replacements))
            {
                for (var i = 0; i < columns.Length; i++)
                {
                    var columnName = columns[i].GetRequiredPropertyNameForColumnValues();
                    var found = false;
                    for (var j = 0; j < values.Length; j++)
                    {
                        if (string.Equals(values[j].Name, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            var holder = new ValueHolder { Value = values[j].Value };
                            replacements[i] = Expression.Field(
                                Expression.Constant(holder), ValueHolder.ValueField);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        ThrowMissingColumn(columnName);
                }
            }

            return BuildLambda(replacements);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPopulatePositional(
            PaginationColumn<T>[] columns,
            ReadOnlySpan<ColumnValue> values,
            Expression[] replacements)
        {
            if (values.Length != columns.Length)
                return false;

            for (var i = 0; i < columns.Length; i++)
            {
                if (!string.Equals(values[i].Name, columns[i].GetRequiredPropertyNameForColumnValues(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            for (var i = 0; i < columns.Length; i++)
            {
                var holder = new ValueHolder { Value = values[i].Value };
                replacements[i] = Expression.Field(
                    Expression.Constant(holder), ValueHolder.ValueField);
            }

            return true;
        }

        [ThreadStatic]
        private static Expression[]? s_replacements;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Expression[] RentReplacements()
        {
            var arr = s_replacements;
            if (arr is not null && arr.Length >= _columnCount)
                return arr;
            arr = new Expression[Math.Max(_columnCount, 4)];
            s_replacements = arr;
            return arr;
        }

        [ThreadStatic]
        private static PlaceholderSubstitutionVisitor? s_visitor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Expression<Func<T, bool>> BuildLambda(Expression[] replacements)
        {
            if (_spineReconstructor is not null)
                return _spineReconstructor.Reconstruct<T>(replacements, _entityParam);

            var visitor = s_visitor ??= new PlaceholderSubstitutionVisitor();
            visitor.Reset(_placeholders, replacements);
            var body = visitor.Visit(_templateBody) ?? throw new InvalidOperationException("Failed to build cached pagination predicate.");
            return FastLambda<T>.Create(body, _entityParam);
        }

        [DoesNotReturn]
        private static void ThrowMissingColumn(string name) =>
            throw new ArgumentException($"No value provided for pagination column '{name}'.");
    }

    /// <summary>
    /// An <see cref="ExpressionVisitor"/> that replaces placeholder <see cref="ParameterExpression"/>
    /// nodes with their corresponding value expressions. Optimized with a type-dispatch short-circuit
    /// in <see cref="Visit"/> to skip leaf node types that can never be placeholders.
    /// </summary>
    private sealed class PlaceholderSubstitutionVisitor : ExpressionVisitor
    {
        private ParameterExpression[] _placeholders = null!;
        private Expression[] _replacements = null!;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(ParameterExpression[] placeholders, Expression[] replacements)
        {
            _placeholders = placeholders;
            _replacements = replacements;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable IDE0072
        public override Expression? Visit(Expression? node)
        {
            if (node is null)
                return null;

            return node.NodeType switch
            {
                ExpressionType.Constant or
                ExpressionType.MemberAccess or
                ExpressionType.Default => node,

                ExpressionType.Parameter => VisitParameter((ParameterExpression)node),

                _ => base.Visit(node),
            };
#pragma warning restore IDE0072
        }

        /// <inheritdoc />
        protected override Expression VisitParameter(ParameterExpression node)
        {
            var placeholders = _placeholders;
            for (var i = 0; i < placeholders.Length; i++)
            {
                if (node == placeholders[i])
                    return _replacements[i];
            }
            return node;
        }
    }
}
