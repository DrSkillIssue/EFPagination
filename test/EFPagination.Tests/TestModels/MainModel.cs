using Microsoft.EntityFrameworkCore;

namespace EFPagination.TestModels;

[Index(nameof(String))]
[Index(nameof(Guid))]
[Index(nameof(IsDone))]
[Index(nameof(Created))]
[Index(nameof(CreatedComputed))]
[Index(nameof(Bytes))]
public class MainModel
{
    public int Id { get; set; }

    public string String { get; set; }

    public Guid Guid { get; set; }

    public bool IsDone { get; set; }

    public DateTime Created { get; set; }

    public DateTime? CreatedNullable { get; set; }

    public DateTime CreatedComputed { get; }

    public TestEnum EnumValue { get; set; }

    public NestedInnerModel Inner { get; set; }

    public List<NestedInner2Model> Inners2 { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays — required for EF Core varbinary mapping.
    public byte[] Bytes { get; set; } = [];
#pragma warning restore CA1819
}

[Index(nameof(Created))]
public class NestedInnerModel
{
    public int Id { get; set; }

    public DateTime Created { get; set; }

    public TestEnum NestedEnumValue { get; set; }
}

public class NestedInner2Model
{
    public int Id { get; set; }

    public int MainModelId { get; set; }

    public MainModel MainModel { get; set; }
}

public enum TestEnum
{
    Value1,
    Value2,
}
