using Sample.Data;
using Sample.Pages.Shared;
using EFPagination;

namespace Sample.Pages;

/// <summary>
/// Demonstrates a composite sort order with mixed directions.
/// Sort order: Created DESC, Id ASC.
/// </summary>
public sealed class CompositeModel(AppDbContext dbContext) : PageModelBase(dbContext)
{
    private static readonly PaginationQueryDefinition<User> s_definition =
        PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

    protected override PaginationQueryDefinition<User> Definition => s_definition;
}
