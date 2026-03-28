using FluentAssertions;
using EFPagination.TestModels;
using Xunit;

namespace EFPagination;

public class TryResolveAndCacheBoundsTests
{
    [Fact]
    public void TryResolve_KnownField_ReturnsTrue()
    {
        var defaultDef = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var createdAsc = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Created).Ascending(x => x.Id));
        var createdDesc = PaginationQuery.Build<MainModel>(b => b.Descending(x => x.Created).Ascending(x => x.Id));

        var registry = new PaginationSortRegistry<MainModel>(defaultDef, [
            new SortField<MainModel>("created", createdAsc, createdDesc)
        ]);

        var found = registry.TryResolve("created", "asc", out var def);

        found.Should().BeTrue();
        def.Should().BeSameAs(createdAsc);
    }

    [Fact]
    public void TryResolve_UnknownField_ReturnsFalse()
    {
        var defaultDef = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var registry = new PaginationSortRegistry<MainModel>(defaultDef);

        var found = registry.TryResolve("nonexistent", "asc", out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_EmptyField_ReturnsTrueWithDefault()
    {
        var defaultDef = PaginationQuery.Build<MainModel>(b => b.Ascending(x => x.Id));
        var registry = new PaginationSortRegistry<MainModel>(defaultDef);

        var found = registry.TryResolve("", "asc", out var def);

        found.Should().BeTrue();
        def.Should().BeSameAs(defaultDef);
    }

    [Fact]
    public void PaginationValues_Create_ProducesUsableInstance()
    {
        var values = PaginationValues<MainModel>.Create(42);

        values.Count.Should().Be(1);
    }

    [Fact]
    public void IncompatibleReferenceException_CustomMessage_PreservesProperties()
    {
        var ex = new IncompatibleReferenceException(
            "Custom message",
            "MissingProp",
            typeof(string),
            typeof(MainModel));

        ex.Message.Should().Be("Custom message");
        ex.PropertyName.Should().Be("MissingProp");
        ex.ReferenceType.Should().Be<string>();
        ex.EntityType.Should().Be<MainModel>();
    }
}
