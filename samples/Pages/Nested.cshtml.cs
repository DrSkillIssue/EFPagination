using Sample.Data;
using Sample.Pages.Shared;
using Microsoft.EntityFrameworkCore;
using EFPagination;

namespace Sample.Pages;

public sealed class NestedModel(AppDbContext dbContext) : PageModelBase(dbContext)
{
    private static readonly PaginationQueryDefinition<User> s_definition =
        PaginationQuery.Build<User>(b => b.Descending(x => x.Details.Created).Ascending(x => x.Id));

    protected override PaginationQueryDefinition<User> Definition => s_definition;

    protected override bool RequiresIncludes => true;

    protected override IQueryable<User> ApplyIncludes(IQueryable<User> query) =>
        query.Include(x => x.Details);
}
