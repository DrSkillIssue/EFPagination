using Microsoft.EntityFrameworkCore;

namespace EFPagination.TestModels;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    private readonly Lock _logLock = new();
    private readonly List<string> _logMessages = [];

    public IEnumerable<string> LogMessages => _logMessages;

    public DbSet<MainModel> MainModels { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        optionsBuilder.LogTo(message =>
        {
            lock (_logLock)
            {
                _logMessages.Add(message);
            }
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var computedPropertyBuilder = modelBuilder.Entity<MainModel>()
            .Property(x => x.CreatedComputed);

        // We're coalescing NULLs into a max date.
        // This results in NULLs effectively being sorted last (if ASC), irrelevant of the Db.
        if (Database.IsSqlServer())
        {
            computedPropertyBuilder
                .HasComputedColumnSql("COALESCE(CreatedNullable, CONVERT(datetime2, '1900-01-01', 102))");
        }
        else
        {
            // For sqlite:
            computedPropertyBuilder
                .HasComputedColumnSql("COALESCE(CreatedNullable, '1900-01-01 00:00:00')");
        }
    }
}
