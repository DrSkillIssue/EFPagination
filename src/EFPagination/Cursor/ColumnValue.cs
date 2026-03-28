namespace EFPagination;

/// <summary>
/// Represents a named pagination boundary value for manual cursor or direct-value APIs.
/// </summary>
/// <param name="Name">The column name or property path associated with <paramref name="Value"/>.</param>
/// <param name="Value">The boundary value for the column, or <see langword="null"/>.</param>
public readonly record struct ColumnValue(string Name, object? Value);
