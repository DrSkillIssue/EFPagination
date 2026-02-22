using Microsoft.EntityFrameworkCore;

namespace Sample.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(x => x.NullableDateComputed)
            // COALESCE pushes NULLs to sort last (ascending). Adjust the SQL for your database provider.
            // This example uses SQLite syntax.
            .HasComputedColumnSql("COALESCE(NullableDate, '9999-12-31 00:00:00')");

        modelBuilder.Entity<User>()
            .HasIndex(x => new { x.NullableDateComputed, x.Id });
    }
}
