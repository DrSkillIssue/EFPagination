using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace EFPagination.Internal;

/// <summary>
/// Writes the structure of a computed column's expression as text that is the same in every process.
/// </summary>
/// <remarks>
/// <see cref="Expression.ToString()"/> is not: it prints parameter names, constants in the current culture and the
/// compiler-generated class of a captured local. This text names node types, types, members and methods, writes
/// constants in the invariant culture, and writes a captured local by its name and value.
/// </remarks>
internal sealed class ColumnShape : ExpressionVisitor
{
    private readonly StringBuilder _text = new();

    /// <summary>
    /// Writes the structure of the body of <paramref name="lambda"/>.
    /// </summary>
    /// <param name="lambda">The column expression.</param>
    /// <returns>The text of the structure.</returns>
    public static string Write(LambdaExpression lambda)
    {
        var shape = new ColumnShape();
        shape.Visit(lambda.Body);
        return shape._text.ToString();
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            _text.Append("null;");
            return node;
        }

        _text.Append(node.NodeType).Append(' ').Append(node.Type.ToString()).Append(';');
        return base.Visit(node);
    }

    protected override Expression VisitParameter(ParameterExpression node) => node;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ConstantExpression { Value: { } closure }
            && closure.GetType().IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            _text.Append(node.Member.Name).Append('=');
            AppendValue(((FieldInfo)node.Member).GetValue(closure));
            return node;
        }

        _text.Append(node.Member.DeclaringType?.ToString()).Append('.').Append(node.Member.Name).Append(';');
        return base.VisitMember(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _text.Append(node.Method.DeclaringType?.ToString()).Append('.').Append(node.Method.Name).Append('(');
        foreach (var parameter in node.Method.GetParameters())
            _text.Append(parameter.ParameterType.ToString()).Append(',');
        _text.Append(");");
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        AppendValue(node.Value);
        return node;
    }

    private void AppendValue(object? value)
    {
        var text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value?.ToString();
        _text.Append(text?.Length ?? -1).Append(':').Append(text).Append(';');
    }
}
