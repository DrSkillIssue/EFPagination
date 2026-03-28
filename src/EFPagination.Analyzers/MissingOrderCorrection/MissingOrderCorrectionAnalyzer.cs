using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EFPagination.Analyzers.MissingOrderCorrection;

/// <summary>
/// Reports when <c>Paginate(..., PaginationDirection.Backward, ...)</c> is called
/// but neither <c>EnsureCorrectOrder</c> nor <c>MaterializeAsync</c> is invoked on
/// the result within the same operation block.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingOrderCorrectionAnalyzer : DiagnosticAnalyzer
{
    private const string ExtensionsTypeFullName = "EFPagination.PaginationExtensions";
    private const string DirectionTypeFullName = "EFPagination.PaginationDirection";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.MissingOrderCorrection];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var extensionsType = compilationContext.Compilation.GetTypeByMetadataName(ExtensionsTypeFullName);
            var directionType = compilationContext.Compilation.GetTypeByMetadataName(DirectionTypeFullName);
            if (extensionsType is null || directionType is null)
                return;

            compilationContext.RegisterOperationBlockAction(blockContext =>
                AnalyzeBlock(blockContext, extensionsType, directionType));
        });
    }

    private static void AnalyzeBlock(
        OperationBlockAnalysisContext context,
        INamedTypeSymbol extensionsType,
        INamedTypeSymbol directionType)
    {
        var backwardPaginateCalls = new List<IInvocationOperation>();
        var hasOrderCorrection = false;

        foreach (var block in context.OperationBlocks)
        {
            foreach (var descendant in block.DescendantsAndSelf())
            {
                if (descendant is not IInvocationOperation invocation)
                    continue;

                var method = invocation.TargetMethod;

                if (method.Name is "EnsureCorrectOrder" or "MaterializeAsync")
                {
                    if (SymbolEqualityComparer.Default.Equals(method.ContainingType, extensionsType))
                        hasOrderCorrection = true;
                    continue;
                }

                if (method.Name is not "Paginate" and not "PaginateQuery")
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, extensionsType))
                    continue;

                if (UsesBackwardDirection(invocation, directionType))
                    backwardPaginateCalls.Add(invocation);
            }
        }

        if (hasOrderCorrection || backwardPaginateCalls.Count == 0)
            return;

        foreach (var call in backwardPaginateCalls)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingOrderCorrection,
                call.Syntax.GetLocation()));
        }
    }

    private static bool UsesBackwardDirection(IInvocationOperation invocation, INamedTypeSymbol directionType)
    {
        foreach (var arg in invocation.Arguments)
        {
            if (!SymbolEqualityComparer.Default.Equals(arg.Value.Type, directionType))
                continue;

            if (arg.Value is IFieldReferenceOperation fieldRef && fieldRef.Field.Name == "Backward")
                return true;

            if (arg.Value.ConstantValue is { HasValue: true, Value: 1 })
                return true;
        }

        return false;
    }
}
