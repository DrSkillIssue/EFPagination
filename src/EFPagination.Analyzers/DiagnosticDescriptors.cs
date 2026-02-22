using Microsoft.CodeAnalysis;

namespace EFPagination.Analyzers;

/// <summary>
/// Diagnostic category constants used by all analyzers in this project.
/// </summary>
public static class DiagnosticCategories
{
    /// <summary>
    /// Category for diagnostics related to incorrect API usage.
    /// </summary>
    public const string Usage = nameof(Usage);
}

/// <summary>
/// Central registry of all <see cref="DiagnosticDescriptor"/> instances
/// emitted by the pagination analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    /// <summary>
    /// <c>KP0001</c>: Reports when a pagination column expression resolves to a nullable type,
    /// which is unsupported for pagination ordering.
    /// </summary>
    public static readonly DiagnosticDescriptor NullablePaginationColumn = new(
        "KP0001",
        "Pagination column may be null",
        "Pagination column may be null",
        DiagnosticCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Nullable columns are not supported in the pagination definition.");
}
