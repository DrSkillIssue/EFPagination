using System.Runtime.InteropServices;
using Sample.Data;
using EFPagination;
using EFPagination.AspNetCore;

namespace Sample.Endpoints;

public static class UsersApi
{
    private static readonly PaginationQueryDefinition<User> s_defaultDefinition =
        PaginationQuery.Build<User>(b => b.Ascending(x => x.Id));

    private static readonly PaginationSortRegistry<User> s_sortRegistry = new(
        s_defaultDefinition,
        SortField.Create<User>("created", "Created"),
        SortField.Create<User>("name", "Name"));

    public static void MapUsersApi(this WebApplication app)
    {
        app.MapGet("/api/users", HandleCursorPagination);
        app.MapGet("/api/users/simple", HandleSimple);
        app.MapGet("/api/users/stream-count", HandleStreamCount);
    }

    private static async Task<IResult> HandleCursorPagination(
        AppDbContext db,
        string? after = null,
        string? before = null,
        int pageSize = 20,
        string? sortBy = null,
        string? sortDir = null)
    {
        var definition = s_sortRegistry.Resolve(sortBy.AsSpan(), sortDir.AsSpan());

        var builder = db.Users.Keyset(definition).MaxPageSize(100).IncludeCount();

        if (before is not null)
            builder = builder.Before(before);
        else if (after is not null)
            builder = builder.After(after);

        var page = await builder.TakeAsync(pageSize);

        var span = CollectionsMarshal.AsSpan(page.Items);
        var items = new UserDto[span.Length];
        for (var i = 0; i < span.Length; i++)
            items[i] = new UserDto(span[i].Id, span[i].Name, span[i].Created);

        return Results.Ok(new PaginatedResponse<UserDto>(
            items, page.NextCursor, page.PreviousCursor,
            page.TotalCount >= 0 ? page.TotalCount : null));
    }

    private static async Task<IResult> HandleSimple(
        AppDbContext db,
        string? after = null,
        int pageSize = 20)
    {
        var builder = db.Users.Keyset(s_defaultDefinition);

        if (after is not null)
            builder = builder.After(after);

        var page = await builder.TakeAsync(pageSize);

        var span = CollectionsMarshal.AsSpan(page.Items);
        var items = new UserDto[span.Length];
        for (var i = 0; i < span.Length; i++)
            items[i] = new UserDto(span[i].Id, span[i].Name, span[i].Created);

        return Results.Ok(new PaginatedResponse<UserDto>(
            items, page.NextCursor, page.PreviousCursor, null));
    }

    private static async Task<IResult> HandleStreamCount(
        AppDbContext db,
        CancellationToken ct)
    {
        var count = 0;
        var pageCount = 0;

        await foreach (var page in db.Users.Keyset(s_defaultDefinition).StreamAsync(500, ct))
        {
            count += page.Count;
            pageCount++;
        }

        return Results.Ok(new { TotalItems = count, Pages = pageCount });
    }
}

public sealed record UserDto(int Id, string Name, DateTime Created);
