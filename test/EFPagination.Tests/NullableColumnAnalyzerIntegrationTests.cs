using System.Collections.Immutable;
using FluentAssertions;
using EFPagination.Analyzers;
using EFPagination.Analyzers.NullableColumn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EFPagination;

public class NullableColumnAnalyzerIntegrationTests
{
    [Fact]
    public async Task Analyzer_ReportsDiagnostic_ForNullablePaginationColumn()
    {
        const string source = """
#nullable enable
using EFPagination;

public sealed class Model
{
    public int? NullableId { get; set; }
}

public static class Usage
{
    public static void Configure(PaginationBuilder<Model> builder)
    {
        builder.Ascending(x => x.NullableId);
    }
}
""";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().ContainSingle(x => x.Id == DiagnosticDescriptors.NullablePaginationColumn.Id);
    }

    [Fact]
    public async Task Analyzer_DoesNotReportDiagnostic_ForNonNullablePaginationColumn()
    {
        const string source = """
#nullable enable
using EFPagination;

public sealed class Model
{
    public int Id { get; set; }
}

public static class Usage
{
    public static void Configure(PaginationBuilder<Model> builder)
    {
        builder.Descending(x => x.Id);
    }
}
""";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        diagnostics.Should().NotContain(x => x.Id == DiagnosticDescriptors.NullablePaginationColumn.Id);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerIntegrationTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new NullableColumnAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        trustedPlatformAssemblies.Should().NotBeNullOrEmpty();

        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(PaginationBuilder<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(NullableColumnAnalyzer).Assembly.Location));
        return [.. references];
    }
}
