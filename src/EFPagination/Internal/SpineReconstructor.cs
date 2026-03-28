using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Reconstructs a predicate expression tree from a pre-analyzed template using a flat
/// instruction sequence with shared subexpression elimination. Each placeholder's
/// Convert node is built once and referenced by all instructions that need it.
/// </summary>
internal sealed class SpineReconstructor
{
    private readonly Instruction[] _instructions;
    private readonly int _resultSlot;

    private SpineReconstructor(Instruction[] instructions, int resultSlot)
    {
        _instructions = instructions;
        _resultSlot = resultSlot;
    }

    public static SpineReconstructor? TryCreate(Expression templateBody, ParameterExpression[] placeholders)
    {
        var ctx = new AnalysisContext(placeholders);
        var resultSlot = Flatten(templateBody, ctx);
        if (resultSlot < 0) return null;
        return new SpineReconstructor([.. ctx.Instructions], resultSlot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Expression<Func<T, bool>> Reconstruct<T>(Expression[] replacements, ParameterExpression entityParam)
    {
        var instructions = _instructions;
        var slots = RentSlots(instructions.Length);

        for (var i = 0; i < instructions.Length; i++)
        {
            ref readonly var inst = ref instructions[i];
            slots[i] = inst.Op switch
            {
                Op.Replacement => replacements[inst.Index],
                Op.Convert => Expression.Convert(slots[inst.Left], inst.Type!),
                Op.Binary => Expression.MakeBinary(inst.ExprType, slots[inst.Left], slots[inst.Right]),
                Op.BinaryStaticLeft => Expression.MakeBinary(inst.ExprType, inst.Static!, slots[inst.Right]),
                Op.BinaryStaticRight => Expression.MakeBinary(inst.ExprType, slots[inst.Left], inst.Static!),
                Op.BinaryBothStatic => inst.Static!,
                Op.Equal => Expression.Equal(inst.Static!, slots[inst.Right]),
                Op.MethodCall => Expression.Call(inst.Static!, inst.Method!, slots[inst.Right]),
                Op.MethodCallCompare => Expression.MakeBinary(inst.ExprType,
                    Expression.Call(inst.Static!, inst.Method!, slots[inst.Right]),
                    FilterPredicateStrategy.ZeroConstant),
                _ => throw new InvalidOperationException()
            };
        }

        var body = slots[_resultSlot];
        return FastLambda<T>.Create(body, entityParam);
    }

    [ThreadStatic]
    private static Expression[]? s_slots;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression[] RentSlots(int count)
    {
        var arr = s_slots;
        if (arr is not null && arr.Length >= count) return arr;
        arr = new Expression[Math.Max(count, 16)];
        s_slots = arr;
        return arr;
    }

    private static int Flatten(Expression node, AnalysisContext ctx)
    {
        switch (node)
        {
            case ParameterExpression param:
            {
                var placeholders = ctx.Placeholders;
                for (var i = 0; i < placeholders.Length; i++)
                {
                    if (param == placeholders[i])
                    {
                        if (ctx.ReplacementSlots[i] >= 0)
                            return ctx.ReplacementSlots[i];
                        var slot = ctx.Emit(new Instruction(Op.Replacement, index: i));
                        ctx.ReplacementSlots[i] = slot;
                        return slot;
                    }
                }
                return -1;
            }

            case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary:
            {
                var operandSlot = Flatten(unary.Operand, ctx);
                if (operandSlot < 0) return -1;

                var key = (operandSlot, unary.Type);
                if (ctx.ConvertCache.TryGetValue(key, out var cached))
                    return cached;

                var slot = ctx.Emit(new Instruction(Op.Convert, left: operandSlot, type: unary.Type));
                ctx.ConvertCache[key] = slot;
                return slot;
            }

            case BinaryExpression binary:
            {
                var leftHas = ContainsPlaceholder(binary.Left, ctx.Placeholders);
                var rightHas = ContainsPlaceholder(binary.Right, ctx.Placeholders);

                if (!leftHas && !rightHas) return -1;

                if (leftHas && rightHas)
                {
                    var leftSlot = Flatten(binary.Left, ctx);
                    var rightSlot = Flatten(binary.Right, ctx);
                    if (leftSlot < 0 || rightSlot < 0) return -1;
                    return ctx.Emit(new Instruction(Op.Binary, exprType: binary.NodeType, left: leftSlot, right: rightSlot));
                }

                if (rightHas)
                {
                    var rightSlot = Flatten(binary.Right, ctx);
                    if (rightSlot < 0) return -1;

                    if (binary.NodeType == ExpressionType.Equal)
                        return ctx.Emit(new Instruction(Op.Equal, exprType: binary.NodeType, right: rightSlot, staticExpr: binary.Left));

                    return ctx.Emit(new Instruction(Op.BinaryStaticLeft, exprType: binary.NodeType, right: rightSlot, staticExpr: binary.Left));
                }

                var leftSlot2 = Flatten(binary.Left, ctx);
                if (leftSlot2 < 0) return -1;
                return ctx.Emit(new Instruction(Op.BinaryStaticRight, exprType: binary.NodeType, left: leftSlot2, staticExpr: binary.Right));
            }

            case MethodCallExpression call when call.Object is not null && call.Arguments.Count == 1:
            {
                var argSlot = Flatten(call.Arguments[0], ctx);
                if (argSlot < 0) return -1;
                return ctx.Emit(new Instruction(Op.MethodCall, right: argSlot, staticExpr: call.Object, method: call.Method));
            }

            default:
                return -1;
        }
    }

    private static bool ContainsPlaceholder(Expression node, ParameterExpression[] placeholders)
    {
        return node switch
        {
            ParameterExpression p => Array.IndexOf(placeholders, p) >= 0,
            BinaryExpression b => ContainsPlaceholder(b.Left, placeholders) || ContainsPlaceholder(b.Right, placeholders),
            UnaryExpression u => ContainsPlaceholder(u.Operand, placeholders),
            MethodCallExpression c => ContainsPlaceholderInCall(c, placeholders),
            _ => false,
        };
    }

    private static bool ContainsPlaceholderInCall(MethodCallExpression call, ParameterExpression[] placeholders)
    {
        for (var i = 0; i < call.Arguments.Count; i++)
            if (ContainsPlaceholder(call.Arguments[i], placeholders)) return true;
        return call.Object is not null && ContainsPlaceholder(call.Object, placeholders);
    }

    private sealed class AnalysisContext(ParameterExpression[] placeholders)
    {
        public readonly ParameterExpression[] Placeholders = placeholders;
        public readonly int[] ReplacementSlots = CreateSlots(placeholders.Length);
        public readonly Dictionary<(int, Type), int> ConvertCache = [];
        public readonly List<Instruction> Instructions = [];

        public int Emit(Instruction inst)
        {
            var slot = Instructions.Count;
            Instructions.Add(inst);
            return slot;
        }

        private static int[] CreateSlots(int count)
        {
            var arr = new int[count];
            Array.Fill(arr, -1);
            return arr;
        }
    }

    private enum Op : byte
    {
        Replacement,
        Convert,
        Binary,
        BinaryStaticLeft,
        BinaryStaticRight,
        BinaryBothStatic,
        Equal,
        MethodCall,
        MethodCallCompare,
    }

    private readonly struct Instruction(Op op, int index = 0, int left = 0, int right = 0,
        ExpressionType exprType = default, Type? type = null,
        Expression? staticExpr = null, System.Reflection.MethodInfo? method = null)
    {
        public readonly Op Op = op;
        public readonly int Index = index;
        public readonly int Left = left;
        public readonly int Right = right;
        public readonly ExpressionType ExprType = exprType;
        public readonly Type? Type = type;
        public readonly Expression? Static = staticExpr;
        public readonly System.Reflection.MethodInfo? Method = method;
    }
}
