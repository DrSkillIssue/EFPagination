using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class ThreadSafetyTests(SqliteDatabaseFixture fixture)
{
    private static readonly int[] s_referenceIds = [10, 30, 50, 70];
    private static readonly PaginationDirection[] s_directions = [PaginationDirection.Forward, PaginationDirection.Backward];

    private readonly SqliteDatabaseFixture _fixture = fixture;

    [Fact]
    public async Task Paginate_ConcurrentCalls_SameDefinition_ProduceIsolatedQueries()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        using var barrier = new Barrier(s_referenceIds.Length);
        var results = new ConcurrentBag<(int referenceId, List<MainModel> items)>();

        var tasks = s_referenceIds.Select(refId => Task.Run(async () =>
        {
            var provider = _fixture.BuildServices();
            var db = provider.GetService<TestDbContext>();

            barrier.SignalAndWait();

            var context = db.MainModels.Paginate(definition, PaginationDirection.Forward, new { Id = refId });
            var items = await context.Query.Take(5).ToListAsync();

            results.Add((refId, items));
        }));

        await Task.WhenAll(tasks);

        foreach (var (referenceId, items) in results)
        {
            items.Should().HaveCount(5);
            items.Should().AllSatisfy(x => x.Id.Should().BeGreaterThan(referenceId,
                $"all items after reference Id={referenceId} should have Id > {referenceId}"));
        }
    }

    [Fact]
    public async Task Paginate_ConcurrentCalls_DifferentDirections_ProduceCorrectResults()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        using var barrier = new Barrier(s_directions.Length);
        var results = new ConcurrentDictionary<PaginationDirection, List<MainModel>>();

        var tasks = s_directions.Select(dir => Task.Run(async () =>
        {
            var provider = _fixture.BuildServices();
            var db = provider.GetService<TestDbContext>();

            barrier.SignalAndWait();

            var context = db.MainModels.Paginate(definition, dir, new { Id = 50 });
            var items = await context.Query.Take(5).ToListAsync();

            if (dir == PaginationDirection.Backward)
                items.Reverse();

            results[dir] = items;
        }));

        await Task.WhenAll(tasks);

        results[PaginationDirection.Forward].Should().AllSatisfy(x => x.Id.Should().BeGreaterThan(50));
        results[PaginationDirection.Backward].Should().AllSatisfy(x => x.Id.Should().BeLessThan(50));
    }
}
