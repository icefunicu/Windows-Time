using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Data;
using System.Data.Common;

namespace ScreenTimeWin.Tests;

public class DataLayerTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbContextOptions<ScreenTimeDbContext> _contextOptions;

    public DataLayerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<ScreenTimeDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ScreenTimeDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Should_Save_And_Retrieve_AppIdentity()
    {
        using var context = new ScreenTimeDbContext(_contextOptions);

        var app = new AppIdentity
        {
            ProcessName = "notepad",
            DisplayName = "Notepad",
            Category = "Utility"
        };

        context.AppIdentities.Add(app);
        context.SaveChanges();

        Assert.NotEqual(Guid.Empty, app.Id);

        var retrieved = context.AppIdentities.FirstOrDefault(a => a.ProcessName == "notepad");
        Assert.NotNull(retrieved);
        Assert.Equal("Notepad", retrieved.DisplayName);
    }

    [Fact]
    public void Should_Save_UsageSession()
    {
        using var context = new ScreenTimeDbContext(_contextOptions);

        var app = new AppIdentity { ProcessName = "test", DisplayName = "Test" };
        context.AppIdentities.Add(app);
        context.SaveChanges();

        var session = new UsageSession
        {
            AppId = app.Id,
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddMinutes(5),
            DurationSeconds = 300
        };

        context.UsageSessions.Add(session);
        context.SaveChanges();

        var count = context.UsageSessions.Count();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Should_Aggregate_Daily_Usage()
    {
        using var context = new ScreenTimeDbContext(_contextOptions);

        var app = new AppIdentity { ProcessName = "agg_test", DisplayName = "Agg Test" };
        context.AppIdentities.Add(app);
        context.SaveChanges();

        var date = DateTime.Today.ToString("yyyy-MM-dd");
        var agg = new DailyAggregate
        {
            DateLocal = date,
            AppId = app.Id,
            TotalSeconds = 100
        };

        context.DailyAggregates.Add(agg);
        context.SaveChanges();

        // Update existing
        var existing = context.DailyAggregates.First(a => a.DateLocal == date && a.AppId == app.Id);
        existing.TotalSeconds += 50;
        context.SaveChanges();

        var updated = context.DailyAggregates.First(a => a.DateLocal == date && a.AppId == app.Id);
        Assert.Equal(150, updated.TotalSeconds);
    }
}
