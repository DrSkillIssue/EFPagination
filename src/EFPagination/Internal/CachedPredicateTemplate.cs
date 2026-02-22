using System.Linq.Expressions;
using System.Runtime.CompilerServices;

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
        object[] referenceValues)
    {
        var template = direction == PaginationDirection.Forward
            ? (_forwardTemplate ??= BuildTemplate(direction))
            : (_backwardTemplate ??= BuildTemplate(direction));
        return template.Instantiate(referenceValues);
    }

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

    /// <summary>
    /// Holds the pre-built template expression tree and pre-allocated per-call resources.
    /// Designed for reuse across calls: <see cref="ValueHolder"/> instances and their corresponding
    /// expression nodes are allocated once at construction and mutated in place on each call.
    /// </summary>
    private sealed class TemplateInstance
    {
        private readonly Expression _templateBody;
        private readonly ParameterExpression _entityParam;
        private readonly PlaceholderSubstitutionVisitor _visitor;
        private readonly ValueHolder[] _valueHolders;
        private readonly Expression[] _valueExpressions;

        /// <summary>
        /// Initializes the template instance, pre-allocating all per-call resources.
        /// </summary>
        /// <param name="templateBody">The expression tree body containing placeholder nodes.</param>
        /// <param name="entityParam">The entity parameter expression for the outer lambda.</param>
        /// <param name="placeholders">The placeholder parameter expressions to be substituted per call.</param>
        /// <param name="columnCount">The number of pagination columns.</param>
        public TemplateInstance(
            Expression templateBody,
            ParameterExpression entityParam,
            ParameterExpression[] placeholders,
            int columnCount)
        {
            _templateBody = templateBody;
            _entityParam = entityParam;

            _valueHolders = new ValueHolder[columnCount];
            _valueExpressions = new Expression[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                _valueHolders[i] = new ValueHolder();
                _valueExpressions[i] = Expression.Field(
                    Expression.Constant(_valueHolders[i]), ValueHolder.ValueField);
            }

            _visitor = new PlaceholderSubstitutionVisitor(placeholders, _valueExpressions);
        }

        /// <summary>
        /// Produces a concrete predicate expression by updating <see cref="ValueHolder.Value"/> fields
        /// and walking the template tree to substitute placeholders with value expressions.
        /// </summary>
        /// <param name="referenceValues">The column values to substitute, one per placeholder.</param>
        /// <returns>A ready-to-use lambda predicate expression.</returns>
        public Expression<Func<T, bool>> Instantiate(object[] referenceValues)
        {
            for (var i = 0; i < referenceValues.Length; i++)
            {
                _valueHolders[i].Value = referenceValues[i];
            }

            var body = _visitor.Visit(_templateBody)!;
            return Expression.Lambda<Func<T, bool>>(body, _entityParam);
        }
    }

    /// <summary>
    /// An <see cref="ExpressionVisitor"/> that replaces placeholder <see cref="ParameterExpression"/>
    /// nodes with their corresponding value expressions. Optimized with a type-dispatch short-circuit
    /// in <see cref="Visit"/> to skip leaf node types that can never be placeholders.
    /// </summary>
    /// <remarks>
    /// Initializes the visitor with the placeholder-to-replacement mapping.
    /// </remarks>
    /// <param name="placeholders">The placeholder parameter expressions to match by reference.</param>
    /// <param name="replacements">The replacement expressions (one per placeholder).</param>
    private sealed class PlaceholderSubstitutionVisitor(
        ParameterExpression[] placeholders,
        Expression[] replacements) : ExpressionVisitor
    {
        private readonly ParameterExpression[] _placeholders = placeholders;
        private readonly Expression[] _replacements = replacements;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable IDE0072 // Populate switch — only 4 of ~85 ExpressionType members are relevant; discard handles the rest
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
