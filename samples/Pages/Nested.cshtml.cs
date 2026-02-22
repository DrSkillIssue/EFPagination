using Sample.Data;
using Sample.Pages.Shared;
using Microsoft.EntityFrameworkCore;
using EFPagination;

namespace Sample.Pages;

/// <summary>
/// Demonstrates pagination on a nested/owned property.
/// Sort order: Details.Created DESC, Id ASC.
/// </summary>
public sealed class NestedModel(AppDbContext dbContext) : PageModelBase(dbContext)
{
    private static readonly PaginationQueryDefinition<User> _definition =
        PaginationQuery.Build<User>(b => b.Descending(x => x.Details.Created).Ascending(x => x.Id));

    protected override PaginationQueryDefinition<User> Definition => _definition;

    protected override IQueryable<User> ApplyIncludes(IQueryable<User> query) =>
        query.Include(x => x.Details);

    protected override Task<User?> GetReferenceAsync(int? after, int? before)
    {
        var id = after ?? before;
        return id is null
            ? Task.FromResult<User?>(null)
            : DbContext.Users.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == id);
    }
}
