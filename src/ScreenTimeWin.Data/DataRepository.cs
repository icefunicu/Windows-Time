using Microsoft.EntityFrameworkCore;
using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Core.Models;

namespace ScreenTimeWin.Data;

public partial class DataRepository
{
    private readonly IDbContextFactory<ScreenTimeDbContext> _contextFactory;

    public DataRepository(IDbContextFactory<ScreenTimeDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task EnsureCreatedAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // Fallback to EnsureCreated as dotnet-ef tool is missing
        await context.Database.EnsureCreatedAsync();
    }

    public async Task<AppIdentity> GetOrAddAppIdentityAsync(string processName, string title)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var app = await context.AppIdentities
            .FirstOrDefaultAsync(a => a.ProcessName == processName);

        if (app == null)
        {
            app = new AppIdentity
            {
                ProcessName = processName,
                DisplayName = !string.IsNullOrEmpty(title) ? title : processName,
                Category = "Unknown"
            };
            context.AppIdentities.Add(app);
            await context.SaveChangesAsync();
        }
        return app;
    }

    public async Task LogSessionAsync(UsageSession session)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.UsageSessions.Add(session);

        // Update daily aggregate
        var date = session.StartUtc.ToLocalTime().ToString("yyyy-MM-dd");
        var agg = await context.DailyAggregates
            .FirstOrDefaultAsync(a => a.DateLocal == date && a.AppId == session.AppId && a.SiteDomain == session.SiteDomain);

        if (agg == null)
        {
            agg = new DailyAggregate
            {
                DateLocal = date,
                AppId = session.AppId,
                SiteDomain = session.SiteDomain,
                TotalSeconds = 0
            };
            context.DailyAggregates.Add(agg);
        }

        agg.TotalSeconds += session.DurationSeconds;
        await context.SaveChangesAsync();
    }

    public async Task<List<DailyAggregate>> GetAggregatesByDateAsync(DateTime date)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dateStr = date.ToString("yyyy-MM-dd");
        return await context.DailyAggregates
            .Include(a => a.App)
            .Where(a => a.DateLocal == dateStr)
            .ToListAsync();
    }

    public async Task<List<UsageSession>> GetSessionsByDateAsync(DateTime date)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).ToUniversalTime();

        // This is a rough approximation, ideally we check overlap
        return await context.UsageSessions
            .Include(a => a.App)
            .Where(s => s.StartUtc >= startUtc && s.StartUtc < endUtc)
            .ToListAsync();
    }

    public async Task<List<LimitRule>> GetRulesAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.LimitRules.Include(r => r.App).ToListAsync();
    }

    public async Task UpsertRuleAsync(LimitRule rule)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.LimitRules.FirstOrDefaultAsync(r => r.AppId == rule.AppId);
        if (existing != null)
        {
            existing.DailyLimitMinutes = rule.DailyLimitMinutes;
            existing.CurfewStartLocal = rule.CurfewStartLocal;
            existing.CurfewEndLocal = rule.CurfewEndLocal;
            existing.ActionOnLimit = rule.ActionOnLimit;
            existing.Enabled = rule.Enabled;
        }
        else
        {
            // Ensure AppId exists or attach? 
            // Usually we are setting rules for existing apps.
            context.LimitRules.Add(rule);
        }
        await context.SaveChangesAsync();
    }

    public async Task<List<AppIdentity>> GetAllAppsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AppIdentities.OrderBy(a => a.DisplayName).ToListAsync();
    }

    /// <summary>
    /// 根据ID获取应用
    /// </summary>
    public async Task<AppIdentity?> GetAppByIdAsync(Guid appId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AppIdentities.FirstOrDefaultAsync(a => a.Id == appId);
    }

    public async Task<long> GetTotalSecondsByDateAsync(DateTime date)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dateStr = date.ToString("yyyy-MM-dd");
        return await context.DailyAggregates
            .Where(a => a.DateLocal == dateStr)
            .SumAsync(a => (long)a.TotalSeconds);
    }

    public async Task<int> GetAppSwitchesCountAsync(DateTime date)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).ToUniversalTime();

        return await context.UsageSessions
            .Where(s => s.StartUtc >= startUtc && s.StartUtc < endUtc)
            .CountAsync();
    }

    public async Task<Dictionary<int, long>> GetHourlyUsageAsync(DateTime date)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).ToUniversalTime();

        var sessions = await context.UsageSessions
            .Where(s => s.StartUtc >= startUtc && s.StartUtc < endUtc)
            .Select(s => new { s.StartUtc, s.DurationSeconds })
            .ToListAsync();

        var hourly = new Dictionary<int, long>();
        for (int i = 0; i < 24; i++) hourly[i] = 0;

        foreach (var s in sessions)
        {
            var sessionStart = s.StartUtc.ToLocalTime();
            var sessionEnd = sessionStart.AddSeconds(s.DurationSeconds);

            var current = sessionStart;
            while (current < sessionEnd)
            {
                if (current.Date != date.Date)
                {
                    // If session spills over to next day, stop counting for today
                    if (current > date.Date.AddDays(1)) break;
                }

                int hour = current.Hour;
                var nextHour = current.Date.AddHours(hour + 1);
                var endOfSegment = nextHour < sessionEnd ? nextHour : sessionEnd;

                var durationInHour = (endOfSegment - current).TotalSeconds;
                if (durationInHour > 0)
                {
                    hourly[hour] += (long)durationInHour;
                }

                current = endOfSegment;
            }
        }
        return hourly;
    }

    public async Task<Dictionary<string, long>> GetCategoryUsageAsync(DateTime date)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dateStr = date.ToString("yyyy-MM-dd");

        var catStats = await context.DailyAggregates
            .Where(a => a.DateLocal == dateStr)
            .Include(a => a.App)
            .GroupBy(a => a.App != null ? a.App.Category : "Uncategorized")
            .Select(g => new { Category = g.Key, Seconds = g.Sum(x => (long)x.TotalSeconds) })
            .ToListAsync();

        return catStats.ToDictionary(x => x.Category ?? "Uncategorized", x => x.Seconds);
    }
}
