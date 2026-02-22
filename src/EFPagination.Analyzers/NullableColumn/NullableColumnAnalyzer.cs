using System.Collections.Immutable;
using EFPagination.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EFPagination.Analyzers.NullableColumn;

/// <summary>
/// Roslyn analyzer that reports <see cref="DiagnosticDescriptors.NullablePaginationColumn"/>
/// when a nullable-typed expression is used as a pagination column in
/// <c>PaginationBuilder&lt;T&gt;.Ascending</c> or <c>Descending</c> calls.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableColumnAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Metadata name for <c>PaginationBuilder&lt;T&gt;</c>, used to locate the builder type at compilation start.
    /// </summary>
    private const string BuilderTypeFullName = "EFPagination.PaginationBuilder`1";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DiagnosticDescriptors.NullablePaginationColumn];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var builderType = compilationContext.Compilation.GetTypeByMetadataName(BuilderTypeFullName);
            if (builderType is null)
            {
                // Library not referenced — nothing to analyze.
                return;
            }

            compilationContext.RegisterOperationAction(
                ctx => AnalyzeInvocation(ctx, (IInvocationOperation)ctx.Operation, builderType),
                OperationKind.Invocation);
        });
    }

    /// <summary>
    /// Inspects an invocation operation to determine whether it is an <c>Ascending</c> or <c>Descending</c>
    /// call on <c>PaginationBuilder&lt;T&gt;</c> whose lambda returns a nullable type.
    /// </summary>
    /// <param name="context">The operation analysis context for reporting diagnostics.</param>
    /// <param name="operation">The invocation operation being analyzed.</param>
    /// <param name="builderType">The resolved <c>PaginationBuilder&lt;T&gt;</c> unbound generic type symbol.</param>
    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        IInvocationOperation operation,
        INamedTypeSymbol builderType)
    {
        var method = operation.TargetMethod;

        // Fast reject: check name before the more expensive symbol comparison.
        if (method.Name is not "Ascending" and not "Descending")
        {
            return;
        }

        if (operation.Arguments.Length == 0)
        {
            return;
        }

        // Compare the unconstructed generic type to avoid per-T comparisons.
        var containingType = method.ContainingType;
        if (containingType is null ||
            !SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, builderType))
        {
            return;
        }

        // Find the first IReturnOperation in the lambda argument without LINQ allocation.
        IReturnOperation? returnOp = null;
        foreach (var descendant in operation.Arguments[0].Descendants())
        {
            if (descendant is IReturnOperation ret)
            {
                returnOp = ret;
                break;
            }
        }

        if (returnOp is null)
        {
            return;
        }

        // Fast path: check the declared type's nullable annotation from the operation model.
        var returnedValue = returnOp.ReturnedValue;
        if (returnedValue?.Type is not null &&
            returnedValue.Type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NullablePaginationColumn,
                returnOp.Syntax.GetLocation()));
            return;
        }

        // Slow path: query the semantic model for nullable flow-state analysis.
        var typeInfo = operation.SemanticModel?.GetTypeInfo(returnOp.Syntax);
        if (typeInfo?.Nullability.FlowState == NullableFlowState.MaybeNull)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NullablePaginationColumn,
                returnOp.Syntax.GetLocation()));
        }
    }
}
