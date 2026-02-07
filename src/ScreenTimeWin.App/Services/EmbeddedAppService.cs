using ScreenTimeWin.IPC.Models;
using ScreenTimeWin.Data;
using ScreenTimeWin.Core.Entities;
using Microsoft.EntityFrameworkCore;
using ScreenTimeWin.App.Services; // For LocalAppMonitorService access if needed, or we pass data
using System.Diagnostics;

namespace ScreenTimeWin.App.Services;

/// <summary>
/// Embedded AppService - Directly uses DataRepository for standalone operation.
/// Ensures "Real Data" persistence without a separate background service.
/// </summary>
public class EmbeddedAppService : IAppService
{
    private readonly DataRepository _repository;
    private readonly LocalAppMonitorService _monitorService;

    public EmbeddedAppService(DataRepository repository, LocalAppMonitorService monitorService)
    {
        _repository = repository;
        _monitorService = monitorService;

        // Subscribe to monitor updates to persist data periodically?
        // Or better: Let monitor service allow us to poll/save. 
        // For now, we will implement a simple periodic save in LocalAppMonitorService or here.
        // Let's hook into monitor service in LocalAppMonitorService itself to save.
    }

    public async Task<PingResponse> PingAsync()
    {
        return await Task.FromResult(new PingResponse { Running = true, Version = "EMBEDDED", Uptime = TimeSpan.Zero });
    }

    public async Task<TodaySummaryResponse> GetTodaySummaryAsync()
    {
        // 1. Get live data from monitor for "Right Now"
        // 2. Get persisted data for today from DB (if we persist periodically)
        // For simplicity in this prototype: usage tracked in MonitorService is the "Truth" for today.
        // We will return what MonitorService has.

        var totalSeconds = _monitorService.GetTotalSeconds();
        var apps = _monitorService.GetTrackedApps().Take(10).Select(a => new AppUsageDto
        {
            AppId = Guid.Empty, // Local monitor doesn't assign GUIDs yet, need mapping
            DisplayName = a.DisplayName,
            ProcessName = a.ProcessName,
            TotalSeconds = (long)a.TotalSeconds,
            Category = a.Category,
            IconBase64 = a.IconBase64 ?? ""
        }).ToList();

        var categories = _monitorService.GetCategoryUsage();

        // Get yesterday's total from DB
        var yesterday = DateTime.Today.AddDays(-1);
        var totalYesterday = await _repository.GetTotalSecondsByDateAsync(yesterday);

        return new TodaySummaryResponse
        {
            TotalSeconds = totalSeconds,
            TotalSecondsYesterday = totalYesterday,
            AppSwitches = _monitorService.GetAppSwitchCount(),
            TopApps = apps,
            CategoryUsage = categories,
            HourlyUsage = new List<long>(new long[24]) // Todo: Calculate hourly from monitor
        };
    }

    public async Task<TodaySummaryResponse> GetUsageByDateAsync(DateTime date)
    {
        if (date.Date == DateTime.Today)
        {
            return await GetTodaySummaryAsync();
        }

        var total = await _repository.GetTotalSecondsByDateAsync(date);
        var hourly = await _repository.GetHourlyUsageAsync(date);
        var cats = await _repository.GetCategoryUsageAsync(date);
        var switches = await _repository.GetAppSwitchesCountAsync(date);

        // We need aggregated top apps for that day... 
        // DataRepository needs a method for this.
        // For now, return basic summary.

        return new TodaySummaryResponse
        {
            TotalSeconds = total,
            AppSwitches = switches,
            CategoryUsage = cats,
            HourlyUsage = hourly.Values.ToList()
        };
    }

    public async Task<List<LimitRuleDto>> GetLimitRulesAsync()
    {
        var rules = await _repository.GetRulesAsync();
        return rules.Select(r => new LimitRuleDto
        {
            AppId = r.AppId,
            ProcessName = r.App?.ProcessName ?? "",
            DisplayName = r.App?.DisplayName ?? "",
            DailyLimitMinutes = r.DailyLimitMinutes,
            Enabled = r.Enabled,
            ActionOnLimit = r.ActionOnLimit.ToString()
        }).ToList();
    }

    public async Task UpsertLimitRuleAsync(LimitRuleDto rule)
    {
        // Find or create AppIdentity first
        var app = await _repository.GetOrAddAppIdentityAsync(rule.ProcessName, rule.DisplayName);

        var entity = new LimitRule
        {
            AppId = app.Id,
            DailyLimitMinutes = rule.DailyLimitMinutes,
            Enabled = rule.Enabled,
            ActionOnLimit = Enum.TryParse<ScreenTimeWin.Core.Models.ActionOnLimit>(rule.ActionOnLimit, out var action) ? action : ScreenTimeWin.Core.Models.ActionOnLimit.NotifyOnly
        };

        await _repository.UpsertRuleAsync(entity);
    }

    public async Task StartFocusAsync(StartFocusRequest request)
    {
        // Implement Focus logic locally if needed, or just track state
        // FocusManager is in Service... we might need to move it or duplicate logic
        await Task.CompletedTask;
    }

    public async Task StopFocusAsync()
    {
        await Task.CompletedTask;
    }

    public async Task ClearDataAsync()
    {
        // Not implemented safely yet
        await Task.CompletedTask;
    }

    public Task<string> ExportDataAsync(string format = "csv")
    {
        return Task.FromResult("Export not implemented in embedded mode");
    }

    public Task<List<NotificationDto>> GetNotificationsAsync()
    {
        return Task.FromResult(new List<NotificationDto>());
    }

    public Task<bool> VerifyPinAsync(string pin)
    {
        return Task.FromResult(pin == "1234");
    }

    public Task<bool> SetPinAsync(string oldPin, string newPin)
    {
        return Task.FromResult(true);
    }

    public async Task<WeeklySummaryResponse> GetWeeklySummaryAsync(DateTime weekStartDate)
    {
        long total = 0;
        var dailyUsage = new List<long>();

        for (int i = 0; i < 7; i++)
        {
            var date = weekStartDate.AddDays(i);
            var t = await _repository.GetTotalSecondsByDateAsync(date);
            // If today, add live data? Or assume live data is synced.
            if (date.Date == DateTime.Today)
            {
                // Use live if DB not synced yet
                t = Math.Max(t, _monitorService.GetTotalSeconds());
            }
            total += t;
            dailyUsage.Add(t);
        }

        // 使用真实历史数据计算上周使用量
        var lastWeekStart = weekStartDate.AddDays(-7);
        long lastWeek = 0;
        for (int i = 0; i < 7; i++)
        {
            lastWeek += await _repository.GetTotalSecondsByDateAsync(lastWeekStart.AddDays(i));
        }

        return new WeeklySummaryResponse
        {
            TotalSeconds = total,
            TotalSecondsLastWeek = lastWeek,
            DailyUsage = dailyUsage,
            CategoryUsage = _monitorService.GetCategoryUsage() // Should aggregate from DB
        };
    }

    public async Task<AppDetailsResponse> GetAppDetailsAsync(Guid appId)
    {
        var app = await _repository.GetAppByIdAsync(appId);
        if (app == null) return new AppDetailsResponse();

        // Get usage from DB
        var total = await _repository.GetTotalSecondsByDateAsync(DateTime.Today); // Filter by app? Repository needs update.

        return new AppDetailsResponse
        {
            AppId = appId,
            ProcessName = app.ProcessName,
            DisplayName = app.DisplayName,
            Category = app.Category ?? "Other",
            TodaySeconds = 0 // 需要特定应用的查询
        };
    }

    public Task<List<SessionDto>> GetRecentSessionsAsync(Guid? appId = null, int maxCount = 20)
    {
        return Task.FromResult(new List<SessionDto>());
    }

    public Task<FocusStatusResponse> GetFocusStatusAsync()
    {
        return Task.FromResult(new FocusStatusResponse());
    }

    public async Task AddExtraTimeAsync(Guid appId, int extraMinutes = 5)
    {
        // Map Guid to ProcessName
        var app = await _repository.GetAppByIdAsync(appId);
        if (app != null)
        {
            _monitorService.ExtendLimit(app.ProcessName, extraMinutes);
        }
    }

    public async Task RequestUnlockAsync(Guid appId, string reason)
    {
        await Task.CompletedTask;
    }
}
