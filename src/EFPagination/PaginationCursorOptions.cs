#pragma warning disable CS1591
namespace EFPagination;

public readonly record struct PaginationCursorOptions(string? SortBy = null, int? TotalCount = null);
