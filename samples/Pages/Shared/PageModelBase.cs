using System.Diagnostics;
using Sample.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EFPagination;

namespace Sample.Pages.Shared;

/// <summary>
/// Base class for pagination sample pages. Encapsulates the common
/// first/last/after/before dispatch logic so each page only defines its sort order.
/// </summary>
public abstract class PageModelBase(AppDbContext dbContext) : PageModel
{
    protected AppDbContext DbContext { get; } = dbContext;

    protected const int PageSize = 20;

    private List<User> _users = [];

    public IReadOnlyList<User> Users => _users;
    public int TotalCount { get; private set; }
    public bool HasPrevious { get; private set; }
    public bool HasNext { get; private set; }
    public long ElapsedMs { get; private set; }
    public long TotalElapsedMs { get; private set; }

    /// <summary>
    /// The prebuilt pagination definition for this page's sort order.
    /// </summary>
    protected abstract PaginationQueryDefinition<User> Definition { get; }

    /// <summary>
    /// Override to add <c>.Include()</c> calls for pages that need related data (e.g. nested properties).
    /// </summary>
    protected virtual IQueryable<User> ApplyIncludes(IQueryable<User> query) => query;

    /// <summary>
    /// Override to customize how the reference entity is loaded (e.g. with <c>.Include()</c>).
    /// </summary>
    protected virtual Task<User?> GetReferenceAsync(int? after, int? before)
    {
        var id = after ?? before;
        return id is null ? Task.FromResult<User?>(null) : DbContext.Users.FindAsync(id.Value).AsTask();
    }

    public async Task OnGetAsync(int? after, int? before, bool first = false, bool last = false)
    {
        var sw = Stopwatch.StartNew();

        var baseQuery = DbContext.Users.AsQueryable();
        TotalCount = await baseQuery.CountAsync();

        var direction = (last || before is not null)
            ? PaginationDirection.Backward
            : PaginationDirection.Forward;

        var reference = (first || last) ? null : await GetReferenceAsync(after, before);

        var context = baseQuery.Paginate(Definition, direction, reference);

        _users = await ApplyIncludes(context.Query)
            .Take(PageSize)
            .ToListAsync();

        context.EnsureCorrectOrder(_users);

        ElapsedMs = sw.ElapsedMilliseconds;

        HasPrevious = await context.HasPreviousAsync(Users);
        HasNext = await context.HasNextAsync(Users);

        TotalElapsedMs = sw.ElapsedMilliseconds;
    }
}
