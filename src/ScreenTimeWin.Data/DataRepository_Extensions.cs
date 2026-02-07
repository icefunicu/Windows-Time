using Microsoft.EntityFrameworkCore;
using System.Text;
using ScreenTimeWin.Core.Entities;

namespace ScreenTimeWin.Data;

public partial class DataRepository
{
    public async Task ClearDataAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // Truncate tables
        // SQLite doesn't support TRUNCATE, use DELETE
        context.UsageSessions.RemoveRange(context.UsageSessions);
        context.DailyAggregates.RemoveRange(context.DailyAggregates);
        // Maybe keep Rules and Apps? Or clear everything?
        // Requirement says "Clear Data", usually implies usage data. 
        // Let's keep settings (Rules, Apps) but clear usage.
        await context.SaveChangesAsync();
    }

    public async Task<string> ExportDataAsync(string format)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // 收集所有数据
        var sessions = await context.UsageSessions.Include(s => s.App).OrderByDescending(s => s.StartUtc).ToListAsync();
        var aggregates = await context.DailyAggregates.OrderByDescending(d => d.DateLocal).ToListAsync();
        var apps = await context.AppIdentities.ToListAsync();
        var rules = await context.LimitRules.ToListAsync();

        string content;
        string extension;

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            // JSON 格式导出
            var exportData = new
            {
                ExportTime = DateTime.Now,
                TotalSessions = sessions.Count,
                TotalApps = apps.Count,
                Sessions = sessions.Select(s => new
                {
                    s.StartUtc,
                    s.EndUtc,
                    s.DurationSeconds,
                    ProcessName = s.App?.ProcessName,
                    DisplayName = s.App?.DisplayName,
                    Category = s.App?.Category,
                    s.WindowTitle,
                    s.SiteDomain
                }),
                DailyAggregates = aggregates.Select(a => new
                {
                    a.DateLocal,
                    a.AppId,
                    a.TotalSeconds,
                    a.SiteDomain
                }),
                Apps = apps.Select(a => new
                {
                    a.Id,
                    a.ProcessName,
                    a.DisplayName,
                    a.Category
                }),
                LimitRules = rules.Select(r => new
                {
                    r.AppId,
                    r.DailyLimitMinutes,
                    r.ActionOnLimit,
                    r.Enabled
                })
            };

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            content = System.Text.Json.JsonSerializer.Serialize(exportData, options);
            extension = "json";
        }
        else
        {
            // CSV 格式导出（默认）
            var sb = new StringBuilder();

            // 会话数据
            sb.AppendLine("# Sessions");
            sb.AppendLine("StartUtc,EndUtc,DurationSeconds,ProcessName,DisplayName,Category,WindowTitle,SiteDomain");
            foreach (var s in sessions)
            {
                sb.AppendLine($"{s.StartUtc:o},{s.EndUtc:o},{s.DurationSeconds},{s.App?.ProcessName},{EscapeCsv(s.App?.DisplayName)},{s.App?.Category},{EscapeCsv(s.WindowTitle)},{s.SiteDomain}");
            }

            sb.AppendLine();
            sb.AppendLine("# Daily Aggregates");
            sb.AppendLine("DateLocal,AppId,TotalSeconds,SiteDomain");
            foreach (var a in aggregates)
            {
                sb.AppendLine($"{a.DateLocal:yyyy-MM-dd},{a.AppId},{a.TotalSeconds},{a.SiteDomain}");
            }

            sb.AppendLine();
            sb.AppendLine("# Apps");
            sb.AppendLine("Id,ProcessName,DisplayName,Category");
            foreach (var a in apps)
            {
                sb.AppendLine($"{a.Id},{a.ProcessName},{EscapeCsv(a.DisplayName)},{a.Category}");
            }

            content = sb.ToString();
            extension = "csv";
        }

        var fileName = $"screentime_export_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
        var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
        await System.IO.File.WriteAllTextAsync(path, content, Encoding.UTF8);

        return path;
    }

    private string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        if (val.Contains(",") || val.Contains("\"") || val.Contains("\n"))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }

    public async Task UpdateAppDetailsAsync(AppIdentity appIdentity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var app = await context.AppIdentities.FindAsync(appIdentity.Id);
        if (app != null)
        {
            if (!string.IsNullOrEmpty(appIdentity.Category)) app.Category = appIdentity.Category;
            if (!string.IsNullOrEmpty(appIdentity.IconBase64)) app.IconBase64 = appIdentity.IconBase64;
            await context.SaveChangesAsync();
        }
    }

    #region FocusSession 持久化

    /// <summary>
    /// 保存专注会话到数据库
    /// </summary>
    public async Task SaveFocusSessionAsync(FocusSession session)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.FocusSessions.Add(session);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 更新专注会话（设置结束时间）
    /// </summary>
    public async Task UpdateFocusSessionAsync(Guid sessionId, DateTime endLocal)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var session = await context.FocusSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.EndLocal = endLocal;
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 获取未完成的专注会话（用于服务重启后恢复）
    /// </summary>
    public async Task<FocusSession?> GetActiveFocusSessionAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // 查找结束时间在未来的会话
        return await context.FocusSessions
            .Where(s => s.EndLocal > DateTime.Now)
            .OrderByDescending(s => s.StartLocal)
            .FirstOrDefaultAsync();
    }

    #endregion
}
