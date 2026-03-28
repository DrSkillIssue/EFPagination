using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

[Collection(SqliteDatabaseCollection.Name)]
public class DiagnosticsTests
{
    private readonly TestDbContext _dbContext;

    public DiagnosticsTests(SqliteDatabaseFixture fixture)
    {
        var provider = fixture.BuildServices();
        _dbContext = provider.GetService<TestDbContext>();
    }

    [Fact]
    public void Paginate_EmitsActivity_WhenListenerRegistered()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "EFPagination",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        _dbContext.MainModels.Paginate(def);

        activities.Should().ContainSingle();
        var activity = activities[0];
        activity.Tags.Should().Contain(t => t.Key == "pagination.entity" && t.Value == "MainModel");
        activity.Tags.Should().Contain(t => t.Key == "pagination.direction" && t.Value == "Forward");
        activity.Tags.Should().Contain(t => t.Key == "pagination.columns");
        activity.Tags.Should().Contain(t => t.Key == "pagination.cached");
    }

    [Fact]
    public void Paginate_NoActivity_WhenNoListenerRegistered()
    {
        var def = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));

        var act = () => _dbContext.MainModels.Paginate(def);

        act.Should().NotThrow();
    }
}
