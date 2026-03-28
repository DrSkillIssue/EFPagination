using Microsoft.EntityFrameworkCore;

namespace EFPagination.Benchmarks;

public sealed class BenchmarkEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime Created { get; set; }
}

public sealed class BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : DbContext(options)
{
    public DbSet<BenchmarkEntity> Items => Set<BenchmarkEntity>();
}

public static class BenchmarkDb
{
    private static bool s_seeded;

    public static BenchmarkDbContext Create()
    {
        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseSqlite("Data Source=bench.db")
            .Options;
        var db = new BenchmarkDbContext(options);

        if (!s_seeded)
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            var baseDate = DateTime.UtcNow.AddYears(-1);
            for (var i = 1; i <= 1000; i++)
                db.Items.Add(new BenchmarkEntity { Id = i, Name = $"Item {i}", Created = baseDate.AddMinutes(i) });
            db.SaveChanges();
            s_seeded = true;
        }

        return db;
    }
}
