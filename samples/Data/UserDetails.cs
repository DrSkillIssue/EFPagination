using Microsoft.EntityFrameworkCore;

namespace Sample.Data;

/// <summary>
/// Related entity used to demonstrate pagination on nested properties.
/// </summary>
[Index(nameof(Created))]
public sealed class UserDetails
{
    public int Id { get; init; }

    public DateTime Created { get; init; }
}
