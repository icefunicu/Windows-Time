using ScreenTimeWin.IPC.Models;

namespace ScreenTimeWin.App.Services;

/// <summary>
/// 应用服务接口 - 定义与后端服务通信的所有API
/// </summary>
public interface IAppService
{
    Task<PingResponse> PingAsync();
    Task<TodaySummaryResponse> GetTodaySummaryAsync();
    Task<TodaySummaryResponse> GetUsageByDateAsync(DateTime date);
    Task<List<LimitRuleDto>> GetLimitRulesAsync();
    Task UpsertLimitRuleAsync(LimitRuleDto rule);
    Task StartFocusAsync(StartFocusRequest request);
    Task StopFocusAsync();
    Task ClearDataAsync();
    Task<string> ExportDataAsync();
    Task<List<NotificationDto>> GetNotificationsAsync();
    Task<bool> VerifyPinAsync(string pin);
    Task<bool> SetPinAsync(string oldPin, string newPin);
    
    // 新增接口
    Task<WeeklySummaryResponse> GetWeeklySummaryAsync(DateTime weekStartDate);
    Task<AppDetailsResponse> GetAppDetailsAsync(Guid appId);
    Task<List<SessionDto>> GetRecentSessionsAsync(Guid? appId = null, int maxCount = 20);
    Task<FocusStatusResponse> GetFocusStatusAsync();
    Task AddExtraTimeAsync(Guid appId, int extraMinutes = 5);
    Task RequestUnlockAsync(Guid appId, string reason);
}

/// <summary>
/// 应用服务实现 - 通过IPC与后端Worker Service通信
/// </summary>
public class AppService : IAppService
{
    private readonly ScreenTimeWin.IPC.IpcClient _client;

    public AppService()
    {
        _client = new ScreenTimeWin.IPC.IpcClient();
    }

    public async Task<PingResponse> PingAsync()
    {
        var result = await _client.SendAsync<PingResponse>(IpcActions.Ping);
        return result ?? new PingResponse { Running = false };
    }

    public async Task<TodaySummaryResponse> GetTodaySummaryAsync()
    {
        var result = await _client.SendAsync<TodaySummaryResponse>(IpcActions.GetTodaySummary);
        return result ?? new TodaySummaryResponse();
    }

    public async Task<TodaySummaryResponse> GetUsageByDateAsync(DateTime date)
    {
        var result = await _client.SendAsync<TodaySummaryResponse>(IpcActions.GetUsageByDate, new UsageByDateRequest { DateLocal = date });
        return result ?? new TodaySummaryResponse();
    }

    public async Task<List<LimitRuleDto>> GetLimitRulesAsync()
    {
        var result = await _client.SendAsync<List<LimitRuleDto>>(IpcActions.GetLimitRules);
        return result ?? new List<LimitRuleDto>();
    }

    public async Task UpsertLimitRuleAsync(LimitRuleDto rule)
    {
        await _client.SendAsync<object>(IpcActions.UpsertLimitRule, rule);
    }

    public async Task StartFocusAsync(StartFocusRequest request)
    {
        await _client.SendAsync<object>(IpcActions.StartFocus, request);
    }

    public async Task StopFocusAsync()
    {
        await _client.SendAsync<object>(IpcActions.StopFocus);
    }

    public async Task ClearDataAsync()
    {
        await _client.SendAsync<object>(IpcActions.ClearData);
    }

    public async Task<string> ExportDataAsync()
    {
        return await _client.SendAsync<string>(IpcActions.ExportData) ?? string.Empty;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync()
    {
        return await _client.SendAsync<List<NotificationDto>>(IpcActions.GetNotifications) ?? new List<NotificationDto>();
    }

    public async Task<bool> VerifyPinAsync(string pin)
    {
        return await _client.SendAsync<bool>(IpcActions.VerifyPin, new PinRequest { Pin = pin });
    }

    public async Task<bool> SetPinAsync(string oldPin, string newPin)
    {
        return await _client.SendAsync<bool>(IpcActions.SetPin, new SetPinRequest { OldPin = oldPin, NewPin = newPin });
    }

    // 新增接口实现
    
    public async Task<WeeklySummaryResponse> GetWeeklySummaryAsync(DateTime weekStartDate)
    {
        var result = await _client.SendAsync<WeeklySummaryResponse>(IpcActions.GetWeeklySummary, new WeeklySummaryRequest { WeekStartDate = weekStartDate });
        return result ?? new WeeklySummaryResponse();
    }

    public async Task<AppDetailsResponse> GetAppDetailsAsync(Guid appId)
    {
        var result = await _client.SendAsync<AppDetailsResponse>(IpcActions.GetAppDetails, new AppDetailsRequest { AppId = appId });
        return result ?? new AppDetailsResponse();
    }

    public async Task<List<SessionDto>> GetRecentSessionsAsync(Guid? appId = null, int maxCount = 20)
    {
        var result = await _client.SendAsync<List<SessionDto>>(IpcActions.GetRecentSessions, new RecentSessionsRequest { AppId = appId, MaxCount = maxCount });
        return result ?? new List<SessionDto>();
    }

    public async Task<FocusStatusResponse> GetFocusStatusAsync()
    {
        var result = await _client.SendAsync<FocusStatusResponse>(IpcActions.GetFocusStatus);
        return result ?? new FocusStatusResponse();
    }

    public async Task AddExtraTimeAsync(Guid appId, int extraMinutes = 5)
    {
        await _client.SendAsync<object>(IpcActions.AddExtraTime, new AddExtraTimeRequest { AppId = appId, ExtraMinutes = extraMinutes });
    }

    public async Task RequestUnlockAsync(Guid appId, string reason)
    {
        await _client.SendAsync<object>(IpcActions.RequestUnlock, new RequestUnlockRequest { AppId = appId, Reason = reason });
    }
}

/// <summary>
/// Mock应用服务 - 用于开发和测试
/// </summary>
public class MockAppService : IAppService
{
    private List<LimitRuleDto> _mockRules = new()
    {
        new LimitRuleDto { AppId = Guid.NewGuid(), DisplayName = "Google Chrome", ProcessName = "chrome", DailyLimitMinutes = 120, Enabled = true },
        new LimitRuleDto { AppId = Guid.NewGuid(), DisplayName = "Steam", ProcessName = "steam", DailyLimitMinutes = 60, Enabled = false }
    };

    public async Task<PingResponse> PingAsync()
    {
        await Task.Delay(50);
        return new PingResponse { Running = true, Version = "MOCK", Uptime = TimeSpan.FromHours(5) };
    }

    public async Task<TodaySummaryResponse> GetTodaySummaryAsync()
    {
        await Task.Delay(100);
        return GenerateMockSummary(DateTime.Now);
    }

    public async Task<TodaySummaryResponse> GetUsageByDateAsync(DateTime date)
    {
        await Task.Delay(100);
        return GenerateMockSummary(date);
    }

    public async Task<List<LimitRuleDto>> GetLimitRulesAsync()
    {
        await Task.Delay(50);
        return _mockRules;
    }

    public async Task UpsertLimitRuleAsync(LimitRuleDto rule)
    {
        await Task.Delay(50);
        var existing = _mockRules.FirstOrDefault(r => r.AppId == rule.AppId);
        if (existing != null)
        {
            existing.DailyLimitMinutes = rule.DailyLimitMinutes;
            existing.Enabled = rule.Enabled;
            existing.ActionOnLimit = rule.ActionOnLimit;
        }
        else
        {
            _mockRules.Add(rule);
        }
    }

    public async Task StartFocusAsync(StartFocusRequest request)
    {
        await Task.Delay(50);
    }

    public async Task StopFocusAsync()
    {
        await Task.Delay(50);
    }

    public async Task ClearDataAsync()
    {
        await Task.Delay(500);
    }

    public async Task<string> ExportDataAsync()
    {
        await Task.Delay(1000);
        return "C:\\Fake\\Path\\export.csv";
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync()
    {
        await Task.Delay(100);
        return new List<NotificationDto>();
    }

    public async Task<bool> VerifyPinAsync(string pin)
    {
        await Task.Delay(50);
        return pin == "1234"; // Mock PIN
    }

    public async Task<bool> SetPinAsync(string oldPin, string newPin)
    {
        await Task.Delay(50);
        return true;
    }

    // 新增接口Mock实现

    public async Task<WeeklySummaryResponse> GetWeeklySummaryAsync(DateTime weekStartDate)
    {
        await Task.Delay(100);
        var rnd = new Random(weekStartDate.DayOfYear);
        return new WeeklySummaryResponse
        {
            TotalSeconds = rnd.Next(50000, 180000),
            TotalSecondsLastWeek = rnd.Next(50000, 180000),
            ChangePercent = (rnd.NextDouble() - 0.5) * 40,
            DailyUsage = Enumerable.Range(0, 7).Select(_ => (long)rnd.Next(3600, 36000)).ToList(),
            CategoryUsage = new Dictionary<string, long>
            {
                { "Work", rnd.Next(20000, 60000) },
                { "Social", rnd.Next(5000, 20000) },
                { "Entertainment", rnd.Next(10000, 40000) },
                { "Learning", rnd.Next(5000, 15000) },
                { "Other", rnd.Next(2000, 10000) }
            },
            TopApps = new List<AppUsageDto>
            {
                new() { DisplayName = "Visual Studio", TotalSeconds = rnd.Next(20000, 50000), ProcessName = "devenv" },
                new() { DisplayName = "Google Chrome", TotalSeconds = rnd.Next(10000, 30000), ProcessName = "chrome" },
                new() { DisplayName = "Spotify", TotalSeconds = rnd.Next(5000, 15000), ProcessName = "spotify" }
            },
            FocusSessionsCompleted = rnd.Next(5, 25),
            FocusTotalSeconds = rnd.Next(3600, 18000)
        };
    }

    public async Task<AppDetailsResponse> GetAppDetailsAsync(Guid appId)
    {
        await Task.Delay(100);
        var rnd = new Random();
        return new AppDetailsResponse
        {
            AppId = appId,
            ProcessName = "chrome",
            DisplayName = "Google Chrome",
            Category = "Browser",
            TodaySeconds = rnd.Next(1800, 7200),
            SevenDayAverageSeconds = rnd.Next(3600, 10800),
            WeekTotalSeconds = rnd.Next(25000, 75000),
            RecentSessions = Enumerable.Range(0, 5).Select(i => new SessionDto
            {
                SessionId = Guid.NewGuid(),
                AppId = appId,
                DisplayName = "Google Chrome",
                StartTimeLocal = DateTime.Now.AddHours(-i * 2),
                EndTimeLocal = DateTime.Now.AddHours(-i * 2 + 0.5),
                DurationSeconds = rnd.Next(300, 1800),
                WindowTitle = $"Tab {i + 1} - Chrome"
            }).ToList(),
            TopTitles = new List<TitleUsageDto>
            {
                new() { Title = "GitHub - Home", TotalSeconds = rnd.Next(600, 1800), SessionCount = 5 },
                new() { Title = "Stack Overflow", TotalSeconds = rnd.Next(300, 900), SessionCount = 3 },
                new() { Title = "YouTube", TotalSeconds = rnd.Next(600, 2400), SessionCount = 4 }
            }
        };
    }

    public async Task<List<SessionDto>> GetRecentSessionsAsync(Guid? appId = null, int maxCount = 20)
    {
        await Task.Delay(100);
        var rnd = new Random();
        return Enumerable.Range(0, Math.Min(maxCount, 10)).Select(i => new SessionDto
        {
            SessionId = Guid.NewGuid(),
            AppId = appId ?? Guid.NewGuid(),
            DisplayName = new[] { "Chrome", "VS Code", "Spotify", "Slack" }[i % 4],
            StartTimeLocal = DateTime.Now.AddHours(-i),
            EndTimeLocal = DateTime.Now.AddHours(-i + 0.3),
            DurationSeconds = rnd.Next(300, 1800),
            WindowTitle = $"Window Title {i + 1}"
        }).ToList();
    }

    public async Task<FocusStatusResponse> GetFocusStatusAsync()
    {
        await Task.Delay(50);
        return new FocusStatusResponse
        {
            IsActive = false,
            RemainingSeconds = 0
        };
    }

    public async Task AddExtraTimeAsync(Guid appId, int extraMinutes = 5)
    {
        await Task.Delay(50);
    }

    public async Task RequestUnlockAsync(Guid appId, string reason)
    {
        await Task.Delay(50);
    }

    private TodaySummaryResponse GenerateMockSummary(DateTime date)
    {
        var rnd = new Random(date.DayOfYear);
        return new TodaySummaryResponse
        {
            TotalSeconds = rnd.Next(3600, 36000),
            TotalSecondsYesterday = rnd.Next(3600, 36000),
            AppSwitches = rnd.Next(20, 150),
            TopApps = new List<AppUsageDto>
            {
                new() { DisplayName = "Visual Studio", TotalSeconds = rnd.Next(3600, 10000), ProcessName = "devenv", Category = "Work" },
                new() { DisplayName = "Google Chrome", TotalSeconds = rnd.Next(1800, 7200), ProcessName = "chrome", Category = "Browser" },
                new() { DisplayName = "Spotify", TotalSeconds = rnd.Next(600, 3600), ProcessName = "spotify", Category = "Entertainment" },
                new() { DisplayName = "Slack", TotalSeconds = rnd.Next(300, 1800), ProcessName = "slack", Category = "Social" },
                new() { DisplayName = "Notepad", TotalSeconds = rnd.Next(60, 600), ProcessName = "notepad", Category = "Work" }
            },
            HourlyUsage = Enumerable.Range(0, 24).Select(_ => (long)rnd.Next(0, 3600)).ToList(),
            CategoryUsage = new Dictionary<string, long>
            {
                { "Work", rnd.Next(5000, 15000) },
                { "Social", rnd.Next(1000, 5000) },
                { "Entertainment", rnd.Next(2000, 8000) },
                { "Browser", rnd.Next(3000, 10000) },
                { "Other", rnd.Next(500, 3000) }
            }
        };
    }
}
