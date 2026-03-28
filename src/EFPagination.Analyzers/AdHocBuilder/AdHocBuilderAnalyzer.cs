using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EFPagination.Analyzers.AdHocBuilder;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AdHocBuilderAnalyzer : DiagnosticAnalyzer
{
    private const string ExtensionsTypeFullName = "EFPagination.PaginationExtensions";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DiagnosticDescriptors.AdHocBuilderInHotPath];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var extensionsType = compilationContext.Compilation.GetTypeByMetadataName(ExtensionsTypeFullName);
            if (extensionsType is null)
                return;

            compilationContext.RegisterOperationAction(
                ctx => AnalyzeInvocation(ctx, (IInvocationOperation)ctx.Operation, extensionsType),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        IInvocationOperation operation,
        INamedTypeSymbol extensionsType)
    {
        var method = operation.TargetMethod;

        if (method.Name is not "Paginate" and not "PaginateQuery")
            return;

        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, extensionsType))
            return;

        foreach (var param in method.Parameters)
        {
            if (param.Type is INamedTypeSymbol { IsGenericType: true, Name: "Action" } actionType
                && actionType.TypeArguments.Length == 1
                && actionType.TypeArguments[0] is INamedTypeSymbol { Name: "PaginationBuilder" })
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AdHocBuilderInHotPath,
                    operation.Syntax.GetLocation()));
                return;
            }
        }
    }
}
