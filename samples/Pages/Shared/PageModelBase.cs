using System.Diagnostics;
using Sample.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using EFPagination;

namespace Sample.Pages.Shared;

public abstract class PageModelBase(AppDbContext dbContext) : PageModel
{
    protected AppDbContext DbContext { get; } = dbContext;

    protected const int PageSize = 20;

    private List<User> _users = [];

    public IReadOnlyList<User> Users => _users;
    public bool HasPrevious { get; private set; }
    public bool HasNext { get; private set; }
    public string? NextCursor { get; private set; }
    public string? PreviousCursor { get; private set; }
    public int TotalCount { get; private set; }
    public long ElapsedMs { get; private set; }

    protected abstract PaginationQueryDefinition<User> Definition { get; }

    protected virtual IQueryable<User> ApplyIncludes(IQueryable<User> query) => query;

    protected virtual bool RequiresIncludes => false;

    public async Task OnGetAsync(string? after = null, string? before = null, bool first = false, bool last = false)
    {
        var start = Stopwatch.GetTimestamp();

        if (RequiresIncludes)
        {
            var direction = (last || before is not null)
                ? PaginationDirection.Backward
                : PaginationDirection.Forward;

            object? reference = null;
            if (!first && !last)
            {
                var id = after ?? before;
                if (id is not null && int.TryParse(id, out var parsed))
                    reference = await DbContext.Users.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == parsed);
            }

            var context = DbContext.Users.Paginate(Definition, direction, reference);

            _users = await ApplyIncludes(context.Query)
                .Take(PageSize)
                .ToListAsync();

            context.EnsureCorrectOrder(_users);
            HasPrevious = await context.HasPreviousAsync(Users);
            HasNext = await context.HasNextAsync(Users);
            TotalCount = await DbContext.Users.CountAsync();
        }
        else
        {
            var builder = DbContext.Users.Keyset(Definition).IncludeCount();

            if (last)
                builder = builder.BeforeEntity(new { Id = int.MaxValue });
            else if (before is not null)
                builder = builder.Before(before);
            else if (after is not null)
                builder = builder.After(after);

            var page = await builder.TakeAsync(PageSize);

            _users = page.Items;
            HasPrevious = page.PreviousCursor is not null;
            HasNext = page.NextCursor is not null;
            NextCursor = page.NextCursor;
            PreviousCursor = page.PreviousCursor;
            TotalCount = page.TotalCount;
        }

        ElapsedMs = Stopwatch.GetElapsedTime(start).Milliseconds;
    }
}
