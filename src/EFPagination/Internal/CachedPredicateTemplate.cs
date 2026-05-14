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
    /// Builds the keyset filter predicate for <paramref name="direction"/> by instantiating the
    /// cached template with the supplied bindings. No boxing; no <see cref="ExpressionType.Convert"/> nodes.
    /// </summary>
    /// <param name="direction">The pagination direction to build for.</param>
    /// <param name="bindings">The typed boundary bindings, one per definition column.</param>
    /// <returns>A predicate lambda suitable for <see cref="Queryable.Where{TSource}(IQueryable{TSource}, Expression{Func{TSource, bool}})"/>.</returns>
    public Expression<Func<T, bool>> Build(
        PaginationDirection direction,
        ColumnBinding[] bindings)
        => GetTemplate(direction).Instantiate(bindings);

    private static readonly string[] s_placeholderNames =
    [
        "__ph_0", "__ph_1", "__ph_2", "__ph_3",
        "__ph_4", "__ph_5", "__ph_6", "__ph_7",
    ];

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
    /// Holds the flattened spine for a fully-templated predicate. Each call to
    /// <c>Instantiate</c> emits the typed field-access expression for each binding and
    /// reconstructs the lambda via <see cref="SpineReconstructor"/>.
    /// </summary>
    internal sealed class TemplateInstance
    {
        private readonly SpineReconstructor _reconstructor;
        private readonly ParameterExpression _entityParam;
        private readonly int _columnCount;

        public TemplateInstance(
            Expression templateBody,
            ParameterExpression entityParam,
            ParameterExpression[] placeholders,
            int columnCount)
        {
            _reconstructor = SpineReconstructor.TryCreate(templateBody, placeholders)
                ?? throw new InvalidOperationException(
                    "Pagination predicate template produced an expression shape that the spine reconstructor cannot flatten.");
            _entityParam = entityParam;
            _columnCount = columnCount;
        }

        public Expression<Func<T, bool>> Instantiate(ColumnBinding[] bindings)
        {
            var replacements = RentReplacements();
            for (var i = 0; i < _columnCount; i++)
                replacements[i] = bindings[i].CreateValueAccessExpression();
            return _reconstructor.Reconstruct<T>(replacements, _entityParam);
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
    }
}
