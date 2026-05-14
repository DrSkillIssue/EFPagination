using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Caches the structural shape of a pagination filter predicate expression tree per direction.
/// Per-call instantiation substitutes the typed per-column placeholders with
/// <see cref="ColumnBinding{TKey}"/>-backed field accesses — no boxing, no <c>Convert</c> nodes.
/// </summary>
/// <typeparam name="T">The entity type being paginated.</typeparam>
internal sealed class CachedPredicateTemplate<T>(PaginationColumn<T>[] columns)
{
    private volatile TemplateInstance? _forwardTemplate;
    private volatile TemplateInstance? _backwardTemplate;
    private readonly PaginationColumn<T>[] _columns = columns;

    /// <summary>
    /// Builds the predicate from already-typed bindings (no boxing, no Convert).
    /// </summary>
    public Expression<Func<T, bool>> Build(
        PaginationDirection direction,
        ColumnBinding[] bindings)
        => GetTemplate(direction).Instantiate(bindings);

    private static readonly string[] s_placeholderNames =
    [
        "__ph_0", "__ph_1", "__ph_2", "__ph_3",
        "__ph_4", "__ph_5", "__ph_6", "__ph_7",
    ];

    /// <summary>
    /// Builds the per-direction template with typed-per-column placeholders.
    /// </summary>
    private TemplateInstance BuildTemplate(PaginationDirection direction)
    {
        var count = _columns.Length;

        var placeholders = new ParameterExpression[count];
        var placeholderExpressions = new Expression[count];
        for (var i = 0; i < count; i++)
        {
            var name = i < s_placeholderNames.Length ? s_placeholderNames[i] : $"__ph_{i}";
            placeholders[i] = Expression.Parameter(_columns[i].Type, name);
            placeholderExpressions[i] = placeholders[i];
        }

        var entityParam = Expression.Parameter(typeof(T), "entity");
        var templateBody = FilterPredicateStrategy.BuildExpressionCore(
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
    /// Holds the pre-built template expression tree. Each call to <c>Instantiate</c> emits the
    /// typed field-access expression for each binding and substitutes it into the template.
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

        public Expression<Func<T, bool>> Instantiate(ColumnBinding[] bindings)
        {
            var replacements = RentReplacements();
            for (var i = 0; i < _columnCount; i++)
                replacements[i] = bindings[i].CreateValueAccessExpression();
            return BuildLambda(replacements);
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

    }

    /// <summary>
    /// An <see cref="ExpressionVisitor"/> that replaces placeholder <see cref="ParameterExpression"/>
    /// nodes with their corresponding value expressions.
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
