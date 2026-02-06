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
        var sessions = await context.UsageSessions.Include(s => s.App).OrderByDescending(s => s.StartUtc).ToListAsync();
        
        var sb = new StringBuilder();
        sb.AppendLine("StartUtc,EndUtc,DurationSeconds,ProcessName,WindowTitle,SiteDomain");
        
        foreach (var s in sessions)
        {
            sb.AppendLine($"{s.StartUtc},{s.EndUtc},{s.DurationSeconds},{s.App?.ProcessName},{EscapeCsv(s.WindowTitle)},{s.SiteDomain}");
        }
        
        var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
        await System.IO.File.WriteAllTextAsync(path, sb.ToString());
        
        return path;
    }

    private string EscapeCsv(string val)
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
}
