using Sample.Data;
using Sample.Pages.Shared;
using EFPagination;

namespace Sample.Pages;

/// <summary>
/// Demonstrates nullable column handling via a database computed column (COALESCE).
/// Sort order: NullableDateComputed ASC, Id ASC.
/// </summary>
public sealed class NullableModel(AppDbContext dbContext) : PageModelBase(dbContext)
{
    private static readonly PaginationQueryDefinition<User> _definition =
        PaginationQuery.Build<User>(b => b.Ascending(x => x.NullableDateComputed).Ascending(x => x.Id));

    protected override PaginationQueryDefinition<User> Definition => _definition;
}
