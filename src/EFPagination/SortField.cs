#pragma warning disable CS1591
namespace EFPagination;

public readonly record struct SortField<T>(
    string Name,
    PaginationQueryDefinition<T> Ascending,
    PaginationQueryDefinition<T> Descending) where T : class;
