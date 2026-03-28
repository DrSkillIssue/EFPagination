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
    /// <value>The diagnostic category name used for usage errors.</value>
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
    /// <value>The descriptor for nullable pagination column usage.</value>
    public static readonly DiagnosticDescriptor NullablePaginationColumn = new(
        "KP0001",
        "Pagination column may be null",
        "Pagination column may be null",
        DiagnosticCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Nullable columns are not supported in the pagination definition.");

    public static readonly DiagnosticDescriptor NonUniqueTiebreaker = new(
        "KP0002",
        "Last pagination column may not be unique",
        "Last pagination column '{0}' may not provide unique ordering; consider adding a unique tiebreaker column (e.g., Id)",
        DiagnosticCategories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The last column in a multi-column pagination definition should be unique to guarantee deterministic page boundaries.");

    public static readonly DiagnosticDescriptor AdHocBuilderInHotPath = new(
        "KP0003",
        "Ad-hoc pagination builder in potential hot path",
        "Consider using a prebuilt PaginationQueryDefinition instead of an ad-hoc Action<PaginationBuilder<T>> for repeated pagination calls",
        DiagnosticCategories.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Using Paginate(Action<PaginationBuilder<T>>) rebuilds column metadata on every call. Prebuilt definitions cache expression tree templates.");

    /// <summary>
    /// <c>KP0004</c>: Reports when backward pagination is used without a subsequent
    /// <c>EnsureCorrectOrder</c> or <c>MaterializeAsync</c> call to fix the result ordering.
    /// </summary>
    /// <value>The descriptor for missing order correction after backward pagination.</value>
    public static readonly DiagnosticDescriptor MissingOrderCorrection = new(
        "KP0004",
        "Backward pagination without order correction",
        "Paginate with PaginationDirection.Backward requires EnsureCorrectOrder or MaterializeAsync to produce correctly ordered results",
        DiagnosticCategories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When paginating backward, results are returned in reverse order and must be corrected via EnsureCorrectOrder or MaterializeAsync.");
}
