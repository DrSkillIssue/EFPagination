using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EFPagination.Analyzers.NonUniqueTiebreaker;

/// <summary>
/// Reports <see cref="DiagnosticDescriptors.NonUniqueTiebreaker"/> when the last column
/// in a multi-column pagination definition does not look like a unique tiebreaker.
/// Heuristic: the last <c>Ascending</c>/<c>Descending</c> call's property name does not
/// end with "Id" and the property's declaring type does not mark it with <c>[Key]</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonUniqueTiebreakerAnalyzer : DiagnosticAnalyzer
{
    private const string BuilderTypeFullName = "EFPagination.PaginationBuilder`1";
    private const string KeyAttributeFullName = "System.ComponentModel.DataAnnotations.KeyAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.NonUniqueTiebreaker];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var builderType = compilationContext.Compilation.GetTypeByMetadataName(BuilderTypeFullName);
            if (builderType is null)
                return;

            var keyAttribute = compilationContext.Compilation.GetTypeByMetadataName(KeyAttributeFullName);

            compilationContext.RegisterOperationBlockAction(blockContext =>
                AnalyzeBlock(blockContext, builderType, keyAttribute));
        });
    }

    private static void AnalyzeBlock(
        OperationBlockAnalysisContext context,
        INamedTypeSymbol builderType,
        INamedTypeSymbol? keyAttribute)
    {
        var lastBuilderCall = FindLastBuilderCall(context, builderType);
        if (lastBuilderCall is null)
            return;

        var propertyName = ExtractPropertyName(lastBuilderCall);
        if (propertyName is null)
            return;

        if (propertyName.EndsWith("Id", System.StringComparison.Ordinal))
            return;

        if (keyAttribute is not null && HasKeyAttribute(lastBuilderCall, keyAttribute))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.NonUniqueTiebreaker,
            lastBuilderCall.Syntax.GetLocation(),
            propertyName));
    }

    private static string? ExtractPropertyName(IInvocationOperation invocation)
    {
        if (invocation.Arguments.Length == 0)
            return null;

        foreach (var descendant in invocation.Arguments[0].Descendants())
        {
            if (descendant is IPropertyReferenceOperation propRef)
                return propRef.Property.Name;
        }

        return null;
    }

    private static bool HasKeyAttribute(IInvocationOperation invocation, INamedTypeSymbol keyAttribute)
    {
        if (invocation.Arguments.Length == 0)
            return false;

        foreach (var descendant in invocation.Arguments[0].Descendants())
        {
            if (descendant is IPropertyReferenceOperation propRef)
            {
                foreach (var attr in propRef.Property.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, keyAttribute))
                        return true;
                }
            }
        }

        return false;
    }

    private static IInvocationOperation? FindLastBuilderCall(
        OperationBlockAnalysisContext context,
        INamedTypeSymbol builderType)
    {
        IInvocationOperation? outermost = null;
        foreach (var block in context.OperationBlocks)
        {
            foreach (var descendant in block.DescendantsAndSelf())
            {
                if (descendant is not IInvocationOperation invocation)
                    continue;

                var method = invocation.TargetMethod;
                if (method.Name is not "Ascending" and not "Descending")
                    continue;

                var containingType = method.ContainingType;
                if (containingType is null ||
                    !SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, builderType))
                    continue;

                if (outermost is null || IsOuterCall(invocation, outermost))
                    outermost = invocation;
            }
        }
        return outermost;
    }

    private static bool IsOuterCall(IInvocationOperation candidate, IInvocationOperation current)
    {
        foreach (var descendant in candidate.DescendantsAndSelf())
        {
            if (descendant == current)
                return true;
        }
        return false;
    }
}
