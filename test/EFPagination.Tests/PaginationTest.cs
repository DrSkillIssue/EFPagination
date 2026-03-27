using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

public abstract class PaginationTest
{
    public enum QueryType
    {
        Id,
        String,
        Guid,
        Bool,
        Created,
        CreatedDesc,
        Nested,
        CreatedDescId,
        IdCreated,
        BoolCreatedId,
        NullCoalescing,
        NullCoalescing2,
        Count,
        Enum,
        NestedEnum,
    }

    private const int Size = 10;

    protected PaginationTest(DatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        DbContext = provider.GetService<TestDbContext>();
    }

    public TestDbContext DbContext { get; }

    public static TheoryData<QueryType> Queries
    {
        get
        {
            var data = new TheoryData<QueryType>();
            foreach (var value in Enum.GetValues<QueryType>())
            {
                data.Add(value);
            }
            return data;
        }
    }

    private static (Func<IQueryable<MainModel>, IQueryable<MainModel>> offsetOrderer, Action<PaginationBuilder<MainModel>> paginationBuilder) GetForQuery(QueryType queryType)
    {
        Func<IQueryable<MainModel>, IQueryable<MainModel>> offsetOrderer = queryType switch
        {
            QueryType.Id => q => q.OrderBy(x => x.Id),
            QueryType.String => q => q.OrderBy(x => x.String),
            QueryType.Guid => q => q.OrderBy(x => x.Guid),
            QueryType.Bool => q => q.OrderBy(x => x.IsDone).ThenBy(x => x.Id),
            QueryType.Created => q => q.OrderBy(x => x.Created),
            QueryType.CreatedDesc => q => q.OrderByDescending(x => x.Created),
            QueryType.Nested => q => q.OrderBy(x => x.Inner.Created),
            QueryType.IdCreated => q => q.OrderBy(x => x.Id).ThenBy(x => x.Created),
            QueryType.BoolCreatedId => q => q.OrderBy(x => x.IsDone).ThenBy(x => x.Created).ThenBy(x => x.Id),
            QueryType.CreatedDescId => q => q.OrderByDescending(x => x.Created).ThenBy(x => x.Id),
            QueryType.NullCoalescing => q => q.OrderBy(x => x.CreatedNullable ?? DateTime.MinValue).ThenBy(x => x.Id),
            QueryType.NullCoalescing2 => q => q.OrderBy(x => x.CreatedNullable ?? x.Created).ThenBy(x => x.Id),
            QueryType.Count => q => q.OrderBy(x => x.Inners2.Count).ThenBy(x => x.Id),
            QueryType.Enum => q => q.OrderBy(x => x.EnumValue).ThenBy(x => x.Id),
            QueryType.NestedEnum => q => q.OrderBy(x => x.Inner.NestedEnumValue).ThenBy(x => x.Id),
            _ => throw new NotImplementedException(),
        };
        Action<PaginationBuilder<MainModel>> paginationBuilder = queryType switch
        {
            QueryType.Id => b => b.Ascending(x => x.Id),
            QueryType.String => b => b.Ascending(x => x.String),
            QueryType.Guid => b => b.Ascending(x => x.Guid),
            QueryType.Bool => b => b.Ascending(x => x.IsDone).Ascending(x => x.Id),
            QueryType.Created => b => b.Ascending(x => x.Created),
            QueryType.CreatedDesc => b => b.Descending(x => x.Created),
            QueryType.Nested => b => b.Ascending(x => x.Inner.Created),
            QueryType.IdCreated => b => b.Ascending(x => x.Id).Ascending(x => x.Created),
            QueryType.BoolCreatedId => b => b.Ascending(x => x.IsDone).Ascending(x => x.Created).Ascending(x => x.Id),
            QueryType.CreatedDescId => b => b.Descending(x => x.Created).Ascending(x => x.Id),
            QueryType.NullCoalescing => b => b.Ascending(x => x.CreatedNullable ?? DateTime.MinValue).Ascending(x => x.Id),
            QueryType.NullCoalescing2 => b => b.Ascending(x => x.CreatedNullable ?? x.Created).Ascending(x => x.Id),
            QueryType.Count => b => b.Ascending(x => x.Inners2.Count).Ascending(x => x.Id),
            QueryType.Enum => q => q.Ascending(x => x.EnumValue).Ascending(x => x.Id),
            QueryType.NestedEnum => q => q.Ascending(x => x.Inner.NestedEnumValue).Ascending(x => x.Id),
            _ => throw new NotImplementedException(),
        };

        return (offsetOrderer, paginationBuilder);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Paginate_Basic(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var expectedResult = await offsetOrderer(DbContext.MainModels)
            .Take(Size)
            .ToListAsync();

        var result = await DbContext.MainModels.PaginateQuery(
            builder)
            .Take(Size)
            .ToListAsync();

        AssertResult(expectedResult, result);
    }

    [Fact]
    public async Task Paginate_Prebuilt()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var result1 = await DbContext.MainModels.PaginateQuery(definition)
            .Take(Size)
            .ToListAsync();

        var result2 = await DbContext.MainModels.PaginateQuery(definition)
            .Take(Size)
            .ToListAsync();

        AssertResult(result1, result2);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Paginate_AfterReference(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var reference = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .Skip(Size)
            .FirstAsync();
        var expectedResult = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .Skip(Size + 1)
            .Take(Size)
            .ToListAsync();

        var result = await DbContext.MainModels.PaginateQuery(
            builder,
            PaginationDirection.Forward,
            reference)
            .IncludeStuff()
            .Take(Size)
            .ToListAsync();

        AssertResult(expectedResult, result);
    }

    [Fact]
    public async Task Paginate_AfterReference_Nested_DtoReference()
    {
        var (offsetOrderer, builder) = GetForQuery(QueryType.Nested);

        var reference = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .Skip(Size)
            .FirstAsync();
        var referenceDto = new
        {
            Inner = new
            {
                reference.Inner.Created,
            },
        };
        var expectedResult = await offsetOrderer(DbContext.MainModels)
            .Skip(Size + 1)
            .Take(Size)
            .ToListAsync();

        var result = await DbContext.MainModels.PaginateQuery(
            builder,
            PaginationDirection.Forward,
            referenceDto)
            .Take(Size)
            .ToListAsync();

        AssertResult(expectedResult, result);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Paginate_BeforeReference(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var reference = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .Skip(Size)
            .FirstAsync();
        var expectedResult = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .Take(Size)
            .ToListAsync();

        var result = await DbContext.MainModels.PaginateQuery(
            builder,
            PaginationDirection.Backward,
            reference)
            .IncludeStuff()
            .Take(Size)
            .ToListAsync();

        AssertResult(expectedResult, result);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Paginate_BeforeFirstReference_Empty(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var reference = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .FirstAsync();

        var result = await DbContext.MainModels.PaginateQuery(
            builder,
            PaginationDirection.Backward,
            reference)
            .Take(Size)
            .ToListAsync();

        result.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task HasPreviousAsync_False(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var context = DbContext.MainModels.Paginate(
            builder);
        var items = await context.Query
            .IncludeStuff()
            .Take(Size)
            .ToListAsync();
        context.EnsureCorrectOrder(items);

        var result = await context.HasPreviousAsync(items);

        result.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task HasPreviousAsync_True(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var reference = await offsetOrderer(DbContext.MainModels)
            .IncludeStuff()
            .Skip(1)
            .FirstAsync();

        var context = DbContext.MainModels.Paginate(
            builder,
            PaginationDirection.Forward,
            reference);
        var items = await context.Query
            .IncludeStuff()
            .Take(Size)
            .ToListAsync();
        context.EnsureCorrectOrder(items);

        var result = await context.HasPreviousAsync(items);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPreviousAsync_Incompatible()
    {
        var context = DbContext.MainModels.Paginate(
            b => b.Ascending(x => x.Id));
        var items = await context.Query
            .Take(20)
            .ToListAsync();
        context.EnsureCorrectOrder(items);

        // A type that doesn't have an Id property which is included in the pagination definition above.
        var dtos = items.Select(x => new { x.Created }).ToList();

        await Assert.ThrowsAsync<IncompatibleReferenceException>(async () => await context.HasPreviousAsync(dtos));
    }

    [Fact]
    public async Task HasPreviousAsync_Incompatible_Nested_ChainPartNull()
    {
        var context = DbContext.MainModels.Paginate(
            b => b.Ascending(x => x.Inner.Id));
        var items = await context.Query
            .Take(20)
            .ToListAsync();
        context.EnsureCorrectOrder(items);

        // Emulate not loading the chain.
        var dtos = items.Select(x => new { Inner = (object)null }).ToList();

        await Assert.ThrowsAsync<IncompatibleReferenceException>(async () => await context.HasPreviousAsync(dtos));
    }

    [Fact]
    public async Task HasPreviousAsync_Null_DoesNotThrow()
    {
        var context = DbContext.MainModels.Paginate(
            // Analyzer would have detected this, but assuming we suppressed the error...
            b => b.Ascending(x => x.CreatedNullable));
        var items = await context.Query
            .Take(20)
            .ToListAsync();
        context.EnsureCorrectOrder(items);

        var dtos = items.Select(x => new { CreatedNullable = (DateTime?)null }).ToList();

        // Shouldn't throw if the user suppressed the analyzer error and knows what they're doing.
        await context.HasPreviousAsync(dtos);
    }

    [Fact]
    public async Task EnsureCorrectOrder_Forward()
    {
        var context = DbContext.MainModels.Paginate(
            b => b.Ascending(x => x.Id),
            PaginationDirection.Forward);
        var items = await context.Query
            .Take(20)
            .ToListAsync();

        context.EnsureCorrectOrder(items);

        Assert.True(items[1].Id > items[0].Id, "Wrong order of ids.");
    }

    [Fact]
    public async Task EnsureCorrectOrder_Backward()
    {
        var context = DbContext.MainModels.Paginate(
            b => b.Ascending(x => x.Id),
            PaginationDirection.Backward);
        var items = await context.Query
            .Take(20)
            .ToListAsync();

        context.EnsureCorrectOrder(items);

        Assert.True(items[1].Id > items[0].Id, "Wrong order of ids.");
    }

    [Fact]
    public async Task Paginate_DbComputed()
    {
        var reference = DbContext.MainModels.OrderBy(x => x.Id).First();

        var result = await DbContext.MainModels.PaginateQuery(
            b => b.Ascending(x => x.CreatedComputed).Ascending(x => x.Id),
            PaginationDirection.Forward,
            reference)
            .Take(Size)
            .ToListAsync();

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HasNext_DbComputed()
    {
        // The last page.
        var context = DbContext.MainModels.Paginate(
            b => b.Ascending(x => x.CreatedComputed).Ascending(x => x.Id),
            PaginationDirection.Backward);
        var data = await context.Query
            .Take(1)
            .ToListAsync();
        context.EnsureCorrectOrder(data);

        // Next on the last page => should be false
        var hasNext = await context.HasNextAsync(data);

        hasNext.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task MultiPage_Forward_NoDuplicatesNoGaps(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);
        var expected = await offsetOrderer(DbContext.MainModels).IncludeStuff().ToListAsync();

        var collected = new List<MainModel>();
        object reference = null!;

        while (true)
        {
            var page = await DbContext.MainModels
                .PaginateQuery(builder, PaginationDirection.Forward, reference)
                .IncludeStuff()
                .Take(Size)
                .ToListAsync();
            if (page.Count == 0) break;
            collected.AddRange(page);
            reference = page[^1];
        }

        collected.Select(x => x.Id).Should().BeEquivalentTo(expected.Select(x => x.Id), o => o.WithStrictOrdering());
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task HasNextAsync_True(QueryType queryType)
    {
        var (_, builder) = GetForQuery(queryType);

        var context = DbContext.MainModels.Paginate(builder);
        var items = await context.Query.IncludeStuff().Take(Size).ToListAsync();
        context.EnsureCorrectOrder(items);

        var result = await context.HasNextAsync(items);

        result.Should().BeTrue(); // 99 rows, took 10 → more exist
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Paginate_WithWhereClause(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);

        var filtered = DbContext.MainModels.Where(x => x.IsDone);
        var reference = await offsetOrderer(filtered).IncludeStuff().Skip(Size).FirstAsync();
        var expected = await offsetOrderer(filtered).IncludeStuff().Skip(Size + 1).Take(Size).ToListAsync();

        var result = await filtered
            .PaginateQuery(builder, PaginationDirection.Forward, reference)
            .IncludeStuff()
            .Take(Size)
            .ToListAsync();

        AssertResult(expected, result);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Paginate_Backward_NoReference_LastPage(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);
        var totalCount = await DbContext.MainModels.CountAsync();

        var expected = await offsetOrderer(DbContext.MainModels)
            .Skip(totalCount - Size)
            .Take(Size)
            .ToListAsync();

        var context = DbContext.MainModels.Paginate(builder, PaginationDirection.Backward);
        var result = await context.Query.Take(Size).ToListAsync();
        context.EnsureCorrectOrder(result);

        AssertResult(expected, result);
    }

    [Fact]
    public async Task HasNextAsync_False_FirstPage_AllData()
    {
        var context = DbContext.MainModels.Paginate(b => b.Ascending(x => x.Id));
        var items = await context.Query.Take(200).ToListAsync(); // 99 rows exist
        context.EnsureCorrectOrder(items);

        var result = await context.HasNextAsync(items);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IncompatibleObjectException_PopulatesProperties()
    {
        var context = DbContext.MainModels.Paginate(b => b.Ascending(x => x.Id));
        var items = await context.Query.Take(5).ToListAsync();
        context.EnsureCorrectOrder(items);

        var dtos = items.Select(x => new { x.Created }).ToList();

        var ex = await Assert.ThrowsAsync<IncompatibleReferenceException>(
            () => context.HasPreviousAsync(dtos));

        ex.PropertyName.Should().Be("Id");
        ex.ReferenceType.Should().NotBeNull();
        ex.EntityType.Should().Be<MainModel>();
    }

    [Fact]
    public async Task EmptyDataset_Forward_ReturnsEmpty()
    {
        var emptyQuery = DbContext.MainModels.Where(x => x.Id < 0);

        var context = emptyQuery.Paginate(b => b.Ascending(x => x.Id));
        var items = await context.Query.Take(Size).ToListAsync();

        items.Should().BeEmpty();
        (await context.HasPreviousAsync(items)).Should().BeFalse();
        (await context.HasNextAsync(items)).Should().BeFalse();
    }

    [Fact]
    public async Task HasPreviousAsync_EmptyList_ReturnsFalse()
    {
        var context = DbContext.MainModels.Paginate(b => b.Ascending(x => x.Id));
        var emptyList = new List<MainModel>();

        var result = await context.HasPreviousAsync(emptyList);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasNextAsync_EmptyList_ReturnsFalse()
    {
        var context = DbContext.MainModels.Paginate(b => b.Ascending(x => x.Id));
        var emptyList = new List<MainModel>();

        var result = await context.HasNextAsync(emptyList);

        result.Should().BeFalse();
    }

    [Fact]
    public void EnsureCorrectOrder_EmptyList_NoOp()
    {
        var context = DbContext.MainModels.Paginate(
            b => b.Ascending(x => x.Id),
            PaginationDirection.Backward);
        var emptyList = new List<MainModel>();

        context.EnsureCorrectOrder(emptyList); // Should not throw

        emptyList.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task Prebuilt_MatchesInline(QueryType queryType)
    {
        var (offsetOrderer, builder) = GetForQuery(queryType);
        var prebuilt = PaginationQuery.Build(builder);

        var reference = await offsetOrderer(DbContext.MainModels).IncludeStuff().Skip(Size).FirstAsync();

        var inlineResult = await DbContext.MainModels
            .PaginateQuery(builder, PaginationDirection.Forward, reference)
            .IncludeStuff().Take(Size).ToListAsync();

        var prebuiltResult = await DbContext.MainModels
            .PaginateQuery(prebuilt, PaginationDirection.Forward, reference)
            .IncludeStuff().Take(Size).ToListAsync();

        prebuiltResult.Select(x => x.Id).Should().BeEquivalentTo(inlineResult.Select(x => x.Id));
    }

    [Fact]
    public async Task StringBuiltDefinition_MissingTiebreakerProperty_ThrowsIncompatibleReferenceException()
    {
        var definition = PaginationQuery.Build<MainModel>("Created", descending: false, tiebreaker: "Id", tiebreakerDescending: false);
        var reference = new { Created = (await DbContext.MainModels.OrderBy(x => x.Created).FirstAsync()).Created };

        var act = () => DbContext.MainModels
            .PaginateQuery(definition, PaginationDirection.Forward, reference)
            .Take(Size)
            .ToListAsync();

        var ex = await Assert.ThrowsAsync<IncompatibleReferenceException>(act);
        ex.PropertyName.Should().Be("Id");
    }

    [Fact]
    public async Task FilteredContext_HasPreviousAndHasNext_AreTrue_ForMiddlePage()
    {
        var filtered = DbContext.MainModels.Where(x => x.IsDone);
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var reference = await filtered.OrderBy(x => x.Id).Skip(9).FirstAsync();

        var context = filtered.Paginate(definition, PaginationDirection.Forward, reference);
        var items = await context.Query.Take(Size).ToListAsync();
        context.EnsureCorrectOrder(items);

        (await context.HasPreviousAsync(items)).Should().BeTrue();
        (await context.HasNextAsync(items)).Should().BeTrue();
    }

    [Fact]
    public async Task PrebuiltDefinition_Supports_StructReference_WithoutLooseTypingBoxingPath()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var result = await DbContext.MainModels
            .PaginateQuery(definition, PaginationDirection.Forward, new MainModelIdReference(10))
            .Take(Size)
            .ToListAsync();

        result.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, Size));
    }

    [Fact]
    public async Task PrebuiltDefinition_Supports_DirectColumnValues()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var reference = await DbContext.MainModels.OrderBy(x => x.Created).ThenBy(x => x.Id).Skip(Size - 1).FirstAsync();

        ColumnValue[] referenceValues =
        [
            new(nameof(MainModel.Id), reference.Id),
            new(nameof(MainModel.Created), reference.Created),
        ];

        var result = await DbContext.MainModels
            .PaginateQuery(definition, PaginationDirection.Forward, referenceValues)
            .Take(Size)
            .ToListAsync();

        result.Select(x => x.Id).Should().BeEquivalentTo(Enumerable.Range(11, Size));
    }

    [Fact]
    public async Task DirectColumnValues_Reject_ComputedColumns()
    {
        var definition = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.CreatedNullable ?? DateTime.MinValue).Ascending(x => x.Id));
        ColumnValue[] referenceValues =
        [
            new(nameof(MainModel.CreatedNullable), DateTime.MinValue),
            new(nameof(MainModel.Id), 10),
        ];

        var act = () => DbContext.MainModels
            .PaginateQuery(definition, PaginationDirection.Forward, referenceValues)
            .Take(Size)
            .ToListAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Direct column-value pagination only supports member-access columns*");
    }

    private readonly record struct MainModelIdReference(int Id);

    private static void AssertResult(List<MainModel> expectedResult, List<MainModel> result)
    {
        result.Should().HaveCount(expectedResult.Count);
        result.Select(x => x.Id).Should().BeEquivalentTo(expectedResult.Select(x => x.Id));
    }
}

public static class MainModelQueryableExtensions
{
    public static IQueryable<MainModel> IncludeStuff(this IQueryable<MainModel> q) => q
        .Include(x => x.Inner)
        .Include(x => x.Inners2);
}

// Run these tests on both SqlServer and Sqlite as a form of a smoke test.

/*
[Collection(SqlServerDatabaseCollection.Name)]
public class SqlServerPaginationTest : PaginationTest
{
    public SqlServerPaginationTest(SqlServerDatabaseFixture fixture)
        : base(fixture)
    {
    }
}
*/

[Collection(SqliteDatabaseCollection.Name)]
public class SqlitePaginationTest(SqliteDatabaseFixture fixture) : PaginationTest(fixture)
{
}
