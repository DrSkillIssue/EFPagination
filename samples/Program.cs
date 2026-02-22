using Sample.Data;
using Sample.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite("Data Source=app.db");
    options.EnableSensitiveDataLogging();
});

var app = builder.Build();

await SeedDataAsync(app);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapUsersApi();

app.Run();

static async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.EnsureDeletedAsync();
    await db.Database.EnsureCreatedAsync();

    const int count = 10_000;
    var random = new Random(42);
    var baseDate = DateTime.UtcNow.AddYears(-1);
    var users = new List<User>(count);

    for (var i = 1; i <= count; i++)
    {
        var created = baseDate.AddMinutes(i).AddSeconds(random.NextDouble() * 50);
        users.Add(new User
        {
            Id = i,
            Name = $"User {i}",
            Created = created,
            NullableDate = i % 2 == 0 ? created : null,
            Details = new UserDetails { Created = created },
        });
    }

    db.Users.AddRange(users);
    await db.SaveChangesAsync();
}
