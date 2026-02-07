using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Text.Json;
using Timer = System.Timers.Timer;
using Microsoft.Extensions.DependencyInjection;

namespace ScreenTimeWin.App.Services;

/// <summary>
/// 本地应用监控服务 - 在App内部实时监控运行中的应用
/// </summary>
public class LocalAppMonitorService : IDisposable
{
    private Timer? _timer;
    private readonly ConcurrentDictionary<int, TrackedApp> _trackedApps = new();
    private int _appSwitchCount;
    private int _lastForegroundPid;
    private Dictionary<string, List<string>> _categoryRules = new();

    // Limit Enforcement
    private List<ScreenTimeWin.IPC.Models.LimitRuleDto> _activeRules = new();
    private readonly HashSet<string> _alertedProcessNames = new();
    private readonly Dictionary<string, int> _temporaryExtensions = new();

    private readonly IServiceScopeFactory? _scopeFactory;

    public LocalAppMonitorService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        LoadCategoryRules();
    }

    public LocalAppMonitorService() // Fallback constructor for tests
    {
        _scopeFactory = null;
        LoadCategoryRules();
    }

    private void LoadCategoryRules()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app-categories.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                _categoryRules = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                                 ?? new Dictionary<string, List<string>>();

                // Normalize to lower case for easier matching
                var normalized = new Dictionary<string, List<string>>();
                foreach (var kvp in _categoryRules)
                {
                    normalized[kvp.Key] = kvp.Value.Select(v => v.ToLowerInvariant()).ToList();
                }
                _categoryRules = normalized;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load category rules: {ex.Message}");
        }
    }

    public void UpdateRules(List<ScreenTimeWin.IPC.Models.LimitRuleDto> rules)
    {
        _activeRules = rules.Where(r => r.Enabled).ToList();
    }

    public void ExtendLimit(string processName, int minutes)
    {
        if (_temporaryExtensions.ContainsKey(processName))
            _temporaryExtensions[processName] += minutes;
        else
            _temporaryExtensions[processName] = minutes;

        if (_alertedProcessNames.Contains(processName))
            _alertedProcessNames.Remove(processName);
    }

    /// <summary>
    /// 监控中的应用列表变更事件
    /// </summary>
    public event EventHandler? AppsUpdated;
    public event EventHandler<LimitReachedEventArgs>? LimitReached;

    // 需要忽略的系统进程
    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost",
        "TextInputHost", "LockApp", "SystemSettings", "WinStore.App",
        "ScreenTimeWin.App", "ApplicationFrameHost", "dwm", "csrss",
        "services", "svchost", "lsass", "wininit", "smss", "System", "Idle"
    };

    /// <summary>
    /// 开始监�?
    /// </summary>
    public void Start(int intervalMs = 2000)
    {
        _timer?.Dispose();
        _timer = new Timer(intervalMs);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();

        // 立即执行一次扫�?
        Task.Run(ScanWindows);
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        ScanWindows();

        // Persist data every 60 seconds (approx 30 ticks of 2s)
        // For prototype, we'll keep it simple and sync occasionally or when app closes.
        // Better: Persist *incrementally* or update DB with current state.

        // Implementation: Just save to DB every ~1 minute
        if (DateTime.Now.Second < 5 && _scopeFactory != null) // Simple check to run approx once a minute
        {
            Task.Run(PersistDataAsync);
        }
    }

    private async Task PersistDataAsync()
    {
        try
        {
            if (_scopeFactory == null) return;
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ScreenTimeWin.Data.DataRepository>();

            var now = DateTime.Now;
            foreach (var app in _trackedApps.Values.Where(a => a.TotalSeconds > 0))
            {
                // We need to log *incremental* usage, but TrackedApp stores Total.
                // In a real app we'd track 'UnsavedSeconds'.
                // For this prototype, we'll just ensure the AppIdentity exists.
                // Proper session logging requires more state tracking.

                await repo.GetOrAddAppIdentityAsync(app.ProcessName, app.DisplayName);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Persistence error: {ex.Message}");
        }
    }

    /// <summary>
    /// 扫描当前所有窗�?
    /// </summary>
    private void ScanWindows()
    {
        try
        {
            var windows = GetAllWindows();
            var now = DateTime.Now;
            var activeProcessIds = new HashSet<int>();

            // 获取前台窗口
            var foregroundHandle = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundHandle, out var foregroundPid);

            foreach (var window in windows)
            {
                if (IgnoredProcesses.Contains(window.ProcessName)) continue;

                activeProcessIds.Add(window.ProcessId);
                bool isForeground = window.ProcessId == (int)foregroundPid;

                if (_trackedApps.TryGetValue(window.ProcessId, out var tracked))
                {
                    // 更新现有追踪
                    var elapsed = (now - tracked.LastSeen).TotalSeconds;
                    if (isForeground)
                    {
                        tracked.ForegroundSeconds += elapsed;
                    }
                    tracked.TotalSeconds += elapsed;
                    tracked.LastSeen = now;
                    tracked.WindowTitle = window.Title;
                    tracked.IsForeground = isForeground;
                }
                else
                {
                    // 新发现的进程
                    var newApp = new TrackedApp
                    {
                        ProcessId = window.ProcessId,
                        ProcessName = window.ProcessName,
                        DisplayName = GetDisplayName(window.ProcessName, window.Title),
                        WindowTitle = window.Title,
                        FilePath = window.FilePath,
                        FirstSeen = now,
                        LastSeen = now,
                        IsForeground = isForeground,
                        Category = DetermineCategory(window.ProcessName)
                    };

                    // 尝试提取图标
                    if (!string.IsNullOrEmpty(window.FilePath))
                    {
                        newApp.IconBase64 = ExtractIconBase64(window.FilePath);
                    }

                    _trackedApps.TryAdd(window.ProcessId, newApp);
                }

                // Check Limits
                if (_trackedApps.TryGetValue(window.ProcessId, out var currentApp))
                {
                    CheckLimit(currentApp);
                }
            }

            // 检测App切换
            if (foregroundPid != 0 && foregroundPid != _lastForegroundPid)
            {
                _appSwitchCount++;
                _lastForegroundPid = (int)foregroundPid;
            }

            // 标记非活动的进程
            foreach (var kvp in _trackedApps)
            {
                if (!activeProcessIds.Contains(kvp.Key))
                {
                    kvp.Value.IsForeground = false;
                    kvp.Value.IsRunning = false;
                }
                else
                {
                    kvp.Value.IsRunning = true;
                }
            }

            AppsUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScanWindows error: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有追踪的应用列表
    /// </summary>
    public IEnumerable<TrackedApp> GetTrackedApps()
    {
        return _trackedApps.Values.OrderByDescending(a => a.TotalSeconds);
    }

    /// <summary>
    /// 获取正在运行的应用（有窗口的�?
    /// </summary>
    public IEnumerable<TrackedApp> GetRunningApps()
    {
        return _trackedApps.Values.Where(a => a.IsRunning).OrderByDescending(a => a.IsForeground).ThenByDescending(a => a.TotalSeconds);
    }

    /// <summary>
    /// 获取今日总使用时长（秒）
    /// </summary>
    public long GetTotalSeconds()
    {
        return (long)_trackedApps.Values.Sum(a => a.TotalSeconds);
    }

    /// <summary>
    /// 获取App切换次数
    /// </summary>
    public int GetAppSwitchCount() => _appSwitchCount;

    /// <summary>
    /// 获取分类使用统计
    /// </summary>
    public Dictionary<string, long> GetCategoryUsage()
    {
        return _trackedApps.Values
            .GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => (long)g.Sum(a => a.TotalSeconds));
    }

    /// <summary>
    /// 清除所有追踪数�?
    /// </summary>
    public void Reset()
    {
        _trackedApps.Clear();
        _appSwitchCount = 0;
        _lastForegroundPid = 0;
    }

    private static string GetDisplayName(string processName, string windowTitle)
    {
        // 对于常见应用返回友好名称
        return processName.ToLowerInvariant() switch
        {
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Firefox",
            "devenv" => "Visual Studio",
            "code" => "VS Code",
            "explorer" => "File Explorer",
            "teams" => "Microsoft Teams",
            "outlook" => "Outlook",
            "excel" => "Excel",
            "winword" => "Word",
            "powerpnt" => "PowerPoint",
            "slack" => "Slack",
            "discord" => "Discord",
            "spotify" => "Spotify",
            "steam" => "Steam",
            "notepad" => "Notepad",
            "notepad++" => "Notepad++",
            "wechat" => "WeChat",
            "qq" => "QQ",
            _ => FormatProcessName(processName)
        };
    }

    private static string FormatProcessName(string processName)
    {
        // 将进程名格式化为更友好的名称
        if (string.IsNullOrEmpty(processName)) return "Unknown";

        // 首字母大�?
        return char.ToUpper(processName[0]) + processName.Substring(1);
    }

    private string DetermineCategory(string processName)
    {
        var lower = processName.ToLowerInvariant();

        // Check configured rules first
        foreach (var rule in _categoryRules)
        {
            if (rule.Value.Contains(lower))
            {
                return rule.Key;
            }
        }

        // Fallback or legacy hardcoded rules (could be removed if json is comprehensive)
        // 浏览�?
        if (new[] { "chrome", "msedge", "firefox", "opera", "brave" }.Contains(lower))
            return "Browser";

        // ... keeping other fallbacks as safety net ...

        return "Other";
    }

    private void CheckLimit(TrackedApp app)
    {
        var rule = _activeRules.FirstOrDefault(r => r.ProcessName.Equals(app.ProcessName, StringComparison.OrdinalIgnoreCase));
        if (rule != null)
        {
            var limitMinutes = rule.DailyLimitMinutes;

            // Add extension
            if (_temporaryExtensions.TryGetValue(app.ProcessName, out var extra))
            {
                limitMinutes += extra;
            }

            if (app.TotalSeconds / 60.0 >= limitMinutes)
            {
                if (!_alertedProcessNames.Contains(app.ProcessName))
                {
                    _alertedProcessNames.Add(app.ProcessName);
                    LimitReached?.Invoke(this, new LimitReachedEventArgs
                    {
                        ProcessId = app.ProcessId,
                        ProcessName = app.ProcessName,
                        AppName = app.DisplayName,
                        IconBase64 = app.IconBase64
                    });
                }
            }
        }
    }

    #region Native Methods

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private static List<WindowInfo> GetAllWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            var title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title)) return true;
            if (title == "Program Manager") return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            try
            {
                var process = Process.GetProcessById((int)pid);
                string path = "";
                try { path = process.MainModule?.FileName ?? ""; } catch { }

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = process.ProcessName,
                    ProcessId = process.Id,
                    FilePath = path
                });
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static string? ExtractIconBase64(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            using var stream = new System.IO.MemoryStream();
            using var bitmap = icon.ToBitmap();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }
        catch
        {
            return null;
        }
    }

    #endregion

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 追踪的应用信�?
/// </summary>
public class TrackedApp
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string? IconBase64 { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public double TotalSeconds { get; set; }
    public double ForegroundSeconds { get; set; }
    public bool IsForeground { get; set; }
    public bool IsRunning { get; set; } = true;
}

/// <summary>
/// 窗口信息
/// </summary>
internal class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string FilePath { get; set; } = string.Empty;
}

public class LimitReachedEventArgs : EventArgs
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string? IconBase64 { get; set; }
}
