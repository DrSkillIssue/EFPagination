using Microsoft.EntityFrameworkCore;

namespace Sample.Data;

/// <summary>
/// Sample entity demonstrating pagination with various column types.
/// </summary>
[Index(nameof(Created), nameof(Id))]
[Index(nameof(Name), nameof(Id))]
public sealed class User
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public DateTime Created { get; init; }

    /// <summary>
    /// Nullable column — demonstrates computed column workaround for pagination ordering.
    /// </summary>
    public DateTime? NullableDate { get; init; }

    /// <summary>
    /// Computed column: <c>COALESCE(NullableDate, '9999-12-31')</c>.
    /// Enables deterministic pagination ordering on a nullable source column.
    /// </summary>
    public DateTime NullableDateComputed { get; }

    /// <summary>
    /// Nested/owned entity — demonstrates pagination on nested properties.
    /// </summary>
    public required UserDetails Details { get; init; }
}
