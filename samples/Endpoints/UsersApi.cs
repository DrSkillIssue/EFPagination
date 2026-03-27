using Sample.Data;
using Microsoft.EntityFrameworkCore;
using EFPagination;

namespace Sample.Endpoints;

/// <summary>
/// Minimal API endpoint demonstrating JSON cursor-based pagination with loose typing.
/// </summary>
public static class UsersApi
{
    private static readonly PaginationQueryDefinition<User> s_definition =
        PaginationQuery.Build<User>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

    public static void MapUsersApi(this WebApplication app)
    {
        app.MapGet("/api/users", async (
            AppDbContext db,
            DateTime? afterCreated,
            int? afterId,
            DateTime? beforeCreated,
            int? beforeId,
            bool first = false,
            bool last = false) =>
        {
            const int pageSize = 20;

            var query = db.Users.AsQueryable();

            var direction = (last || beforeCreated is not null)
                ? PaginationDirection.Backward
                : PaginationDirection.Forward;

            // LOOSE TYPING: Build a reference object from query parameters instead of loading the entity.
            // The library only needs an object with properties matching the pagination columns.
            object? reference = null;
            if (afterCreated is not null && afterId is not null)
            {
                reference = new { Created = afterCreated.Value, Id = afterId.Value };
            }
            else if (beforeCreated is not null && beforeId is not null)
            {
                reference = new { Created = beforeCreated.Value, Id = beforeId.Value };
            }

            var context = query.Paginate(s_definition, direction, reference);

            var users = await context.Query
                .Take(pageSize)
                .Select(u => new UserDto(u.Id, u.Name, u.Created))
                .ToListAsync();

            // LOOSE TYPING: EnsureCorrectOrder and HasPrevious/HasNext work with DTOs — not just entities.
            context.EnsureCorrectOrder(users);
            var hasPrevious = await context.HasPreviousAsync(users);
            var hasNext = await context.HasNextAsync(users);

            var firstItem = users.Count > 0 ? users[0] : null;
            var lastItem = users.Count > 0 ? users[^1] : null;

            return Results.Ok(new PagedResponse<UserDto>(
                Data: users,
                HasPrevious: hasPrevious,
                HasNext: hasNext,
                Cursors: new CursorPair(
                    Before: firstItem is not null ? new CursorToken(firstItem.Created, firstItem.Id) : null,
                    After: lastItem is not null ? new CursorToken(lastItem.Created, lastItem.Id) : null)));
        });
    }
}

/// <summary>Projected user DTO — demonstrates loose typing with projected results.</summary>
public sealed record UserDto(int Id, string Name, DateTime Created);

/// <summary>Cursor token containing the pagination column values for a single row.</summary>
public sealed record CursorToken(DateTime Created, int Id);

/// <summary>Cursor pair for the first and last items in the current page.</summary>
public sealed record CursorPair(CursorToken? Before, CursorToken? After);

/// <summary>Paged response envelope with cursor-based navigation tokens.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, bool HasPrevious, bool HasNext, CursorPair Cursors);
