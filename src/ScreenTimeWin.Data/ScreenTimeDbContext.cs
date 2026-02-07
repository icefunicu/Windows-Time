using Microsoft.EntityFrameworkCore;
using ScreenTimeWin.Core.Entities;

namespace ScreenTimeWin.Data;

public class ScreenTimeDbContext : DbContext
{
    public DbSet<AppIdentity> AppIdentities { get; set; } = null!;
    public DbSet<UsageSession> UsageSessions { get; set; } = null!;
    public DbSet<DailyAggregate> DailyAggregates { get; set; } = null!;
    public DbSet<LimitRule> LimitRules { get; set; } = null!;
    public DbSet<GlobalSetting> GlobalSettings { get; set; } = null!;
    public DbSet<FocusSession> FocusSessions { get; set; } = null!;
    public DbSet<InterceptEvent> InterceptEvents { get; set; } = null!;

    public string DbPath { get; }

    public ScreenTimeDbContext(DbContextOptions<ScreenTimeDbContext> options) : base(options)
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbFolder = System.IO.Path.Join(path, "ScreenTimeWin");
        DbPath = System.IO.Path.Join(dbFolder, "ScreenTimeWin.db");
    }

    // For design-time creation if needed, though we usually use DI.
    public ScreenTimeDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbFolder = System.IO.Path.Join(path, "ScreenTimeWin");
        DbPath = System.IO.Path.Join(dbFolder, "ScreenTimeWin.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyAggregate>()
            .Property(da => da.SiteDomain)
            .HasDefaultValue("");

        modelBuilder.Entity<DailyAggregate>()
            .HasKey(da => new { da.DateLocal, da.AppId, da.SiteDomain });

        modelBuilder.Entity<AppIdentity>().HasIndex(a => a.ProcessName).IsUnique();
        modelBuilder.Entity<GlobalSetting>().HasIndex(s => s.Key).IsUnique();

        // SQLite doesn't support TimeSpan natively in older EF Core versions, but 8 should be fine.
        // Just in case, we can convert if issues arise, but standard SQLite provider handles it as TEXT usually.
    }
}
