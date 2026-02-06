using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;

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

    /// <summary>
    /// 监控中的应用列表变更事件
    /// </summary>
    public event EventHandler? AppsUpdated;

    // 需要忽略的系统进程
    private static readonly HashSet<string> IgnoredProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost",
        "TextInputHost", "LockApp", "SystemSettings", "WinStore.App",
        "ScreenTimeWin.App", "ApplicationFrameHost", "dwm", "csrss",
        "services", "svchost", "lsass", "wininit", "smss", "System", "Idle"
    };

    /// <summary>
    /// 开始监控
    /// </summary>
    public void Start(int intervalMs = 2000)
    {
        _timer?.Dispose();
        _timer = new Timer(intervalMs);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
        
        // 立即执行一次扫描
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
    }

    /// <summary>
    /// 扫描当前所有窗口
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
    /// 获取正在运行的应用（有窗口的）
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
    /// 清除所有追踪数据
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
        
        // 首字母大写
        return char.ToUpper(processName[0]) + processName.Substring(1);
    }

    private static string DetermineCategory(string processName)
    {
        var lower = processName.ToLowerInvariant();
        
        // 浏览器
        if (new[] { "chrome", "msedge", "firefox", "opera", "brave" }.Contains(lower))
            return "Browser";
        
        // 办公
        if (new[] { "excel", "winword", "powerpnt", "outlook", "teams", "slack", "onenote" }.Contains(lower))
            return "Work";
        
        // 开发工具
        if (new[] { "devenv", "code", "rider", "idea64", "webstorm64", "pycharm64", "notepad++", "sublime_text" }.Contains(lower))
            return "Development";
        
        // 社交
        if (new[] { "wechat", "qq", "telegram", "discord", "whatsapp" }.Contains(lower))
            return "Social";
        
        // 娱乐
        if (new[] { "steam", "spotify", "vlc", "potplayer", "mpc-hc64" }.Contains(lower))
            return "Entertainment";
        
        return "Other";
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
/// 追踪的应用信息
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
