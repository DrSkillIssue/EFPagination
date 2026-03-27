using Sample.Data;
using Sample.Pages.Shared;
using EFPagination;

namespace Sample.Pages;

/// <summary>
/// Demonstrates the RECOMMENDED approach: a prebuilt <see cref="PaginationQueryDefinition{T}"/>.
/// Sort order: Id ASC (single column).
/// </summary>
public sealed class IndexModel(AppDbContext dbContext) : PageModelBase(dbContext)
{
    private static readonly PaginationQueryDefinition<User> s_definition =
        PaginationQuery.Build<User>(b => b.Ascending(x => x.Id));

    protected override PaginationQueryDefinition<User> Definition => s_definition;
}
