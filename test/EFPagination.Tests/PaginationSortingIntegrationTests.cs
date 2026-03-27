using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class PaginationSortingIntegrationTests
{
    private readonly TestDbContext _dbContext;

    public PaginationSortingIntegrationTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public async Task Build_FromPropertyName_MatchesBuilderDefinition()
    {
        var stringDefinition = PaginationQuery.Build<MainModel>("Created", descending: true, tiebreaker: "Id", tiebreakerDescending: false);
        var builderDefinition = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

        var stringResult = await _dbContext.MainModels
            .PaginateQuery(stringDefinition)
            .Take(10)
            .ToListAsync();

        var builderResult = await _dbContext.MainModels
            .PaginateQuery(builderDefinition)
            .Take(10)
            .ToListAsync();

        stringResult.Select(x => x.Id).Should().BeEquivalentTo(builderResult.Select(x => x.Id), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task SortRegistry_ResolvesRequestedSort_AndFallsBackToDefault()
    {
        var defaultDefinition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        SortField<MainModel>[] fields =
        [
            new(
                "created",
                PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id)),
                PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id)))
        ];

        var registry = new PaginationSortRegistry<MainModel>(defaultDefinition, fields);

        var createdDesc = registry.Resolve("CrEaTeD".AsSpan(), "DESC".AsSpan());
        var fallback = registry.Resolve("unknown".AsSpan(), "asc".AsSpan());

        var createdDescPage = await _dbContext.MainModels.PaginateQuery(createdDesc).Take(10).ToListAsync();
        var fallbackPage = await _dbContext.MainModels.PaginateQuery(fallback).Take(10).ToListAsync();

        createdDescPage.Select(x => x.Id).Should().BeEquivalentTo(
            await _dbContext.MainModels.OrderByDescending(x => x.Created).ThenBy(x => x.Id).Take(10).Select(x => x.Id).ToListAsync(),
            options => options.WithStrictOrdering());

        fallbackPage.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task SortRegistry_ResolvedDefinition_PaginatesAcrossMultiplePages()
    {
        var defaultDefinition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        SortField<MainModel>[] fields =
        [
            new(
                "created",
                PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id)),
                PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id)))
        ];

        var registry = new PaginationSortRegistry<MainModel>(defaultDefinition, fields);
        var definition = registry.Resolve("created".AsSpan(), "desc".AsSpan());

        var firstPage = await _dbContext.MainModels
            .PaginateQuery(definition)
            .Take(10)
            .ToListAsync();

        var secondPage = await _dbContext.MainModels
            .PaginateQuery(definition, PaginationDirection.Forward, firstPage[^1])
            .Take(10)
            .ToListAsync();

        var expected = await _dbContext.MainModels
            .OrderByDescending(x => x.Created)
            .ThenBy(x => x.Id)
            .Skip(10)
            .Take(10)
            .Select(x => x.Id)
            .ToListAsync();

        secondPage.Select(x => x.Id).Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Build_FromPropertyName_WithoutTiebreaker_UsesPrimarySort()
    {
        var definition = PaginationQuery.Build<MainModel>("Created", descending: false, tiebreaker: null);

        var page = await _dbContext.MainModels
            .PaginateQuery(definition)
            .Take(10)
            .Select(x => x.Id)
            .ToListAsync();

        page.Should().BeEquivalentTo(
            await _dbContext.MainModels.OrderBy(x => x.Created).Take(10).Select(x => x.Id).ToListAsync(),
            options => options.WithStrictOrdering());
    }
}
