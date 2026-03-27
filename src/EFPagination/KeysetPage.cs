#pragma warning disable CS1591
#pragma warning disable CA1002
namespace EFPagination;

public readonly record struct KeysetPage<T>(
    List<T> Items,
    bool HasMore,
    int TotalCount);
