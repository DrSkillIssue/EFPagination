using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFPagination.TestModels;

namespace EFPagination;

public abstract class DatabaseFixture : IDisposable
{
    private static bool s_initialized;

    protected DatabaseFixture()
    {
        SetupDatabase();
    }

    public IServiceProvider BuildServices(Action<IServiceCollection> configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
        {
            ConfigureDb(options);
            options.EnableSensitiveDataLogging();
        });
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    protected abstract void ConfigureDb(DbContextOptionsBuilder options);

    public IServiceProvider BuildForService<T>(Action<IServiceCollection> configureServices = null)
        where T : class
    {
        return BuildServices(services =>
        {
            configureServices?.Invoke(services);
            services.AddTransient<T>();
        });
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void SetupDatabase()
    {
        if (s_initialized) return;

        var provider = BuildServices();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetService<TestDbContext>();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        Seed(context);

        s_initialized = true;
    }

    private static void Seed(TestDbContext context)
    {
        var now = DateTime.Now.AddYears(-1);

        for (var i = 1; i < 100; i++)
        {
            var created = now.AddMinutes(i);
            // Deterministic pseudo-random count without System.Random (avoids CA5394).
            var inners2Count = (i * 7 + 3) % 10;
            _ = context.MainModels.Add(new MainModel
            {
                String = i.ToString(),
                Guid = Guid.NewGuid(),
                IsDone = i % 2 == 0,
                Created = created,
                CreatedNullable = i % 2 == 0 ? created : null,
                Inner = new NestedInnerModel
                {
                    Created = created,
                    NestedEnumValue = i % 2 == 0 ? TestEnum.Value1 : TestEnum.Value2,
                },
                Inners2 = Enumerable.Range(0, inners2Count).Select(_ => new NestedInner2Model()).ToList(),
                EnumValue = i % 2 == 0 ? TestEnum.Value1 : TestEnum.Value2,
            });
        }

        context.SaveChanges();
    }
}

public class SqlServerDatabaseFixture : DatabaseFixture
{
    protected override void ConfigureDb(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(
              "Server=(localdb)\\mssqllocaldb;Database=EFPaginationTest;Trusted_Connection=True;MultipleActiveResultSets=true");
    }
}

public class SqliteDatabaseFixture : DatabaseFixture
{
    protected override void ConfigureDb(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=test.db");
}
