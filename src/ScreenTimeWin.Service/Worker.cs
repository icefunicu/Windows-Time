using System.Runtime.Versioning;
using System.Windows.Automation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Data;

namespace ScreenTimeWin.Service;

[SupportedOSPlatform("windows")]
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DataRepository _repository;
    private readonly FocusManager _focusManager;
    private readonly NotificationQueue _notificationQueue;
    private readonly CurrentSessionState _currentSessionState;
    private UsageSession? _currentSession;

    public Worker(ILogger<Worker> logger, DataRepository repository, FocusManager focusManager, NotificationQueue notificationQueue, CurrentSessionState currentSessionState)
    {
        _logger = logger;
        _repository = repository;
        _focusManager = focusManager;
        _notificationQueue = notificationQueue;
        _currentSessionState = currentSessionState;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _repository.EnsureCreatedAsync();
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // 1. Get Active (Foreground) Window Info ONLY
                var (processName, pid, filePath) = NativeHelper.GetActiveProcessInfo();
                var windowTitle = NativeHelper.GetActiveWindowTitle();

                int activePid = 0;

                if (pid > 0 && !IsIgnored(processName))
                {
                    activePid = pid;

                    // Get or create session for this active window
                    if (!_currentSessionState.ActiveSessions.TryGetValue(pid, out var session))
                    {
                        // New session
                        var app = await _repository.GetOrAddAppIdentityAsync(processName, windowTitle);

                        // Basic auto-cat & icon logic
                        if (string.IsNullOrEmpty(app.Category))
                        {
                            app.Category = DetermineCategory(processName);
                            await _repository.UpdateAppDetailsAsync(app);
                        }

                        if (!string.IsNullOrEmpty(filePath))
                        {
                            bool isSmall = string.IsNullOrEmpty(app.IconBase64) || app.IconBase64.Length < 3000;
                            if (isSmall)
                            {
                                var iconBase64 = JumboIconHelper.ExtractJumboIconBase64(filePath);
                                if (string.IsNullOrEmpty(iconBase64))
                                {
                                    iconBase64 = NativeHelper.ExtractIconBase64(filePath);
                                }

                                if (!string.IsNullOrEmpty(iconBase64) && iconBase64 != app.IconBase64)
                                {
                                    app.IconBase64 = iconBase64;
                                    await _repository.UpdateAppDetailsAsync(app);
                                }
                            }
                        }

                        // Start new session
                        session = new UsageSession
                        {
                            AppId = app.Id,
                            App = app,
                            WindowTitle = windowTitle,
                            StartUtc = now,
                            EndUtc = now
                        };
                        _currentSessionState.ActiveSessions.TryAdd(pid, session);
                        _currentSession = session;
                    }
                    else
                    {
                        // Update existing session
                        session.EndUtc = now;
                        session.DurationSeconds = (int)(now - session.StartUtc).TotalSeconds;

                        if (session.WindowTitle != windowTitle)
                        {
                            session.WindowTitle = windowTitle;
                        }
                        _currentSession = session;
                    }

                    // 2. CHECK LIMITS IMMEDIATELY
                    if (session.AppId != Guid.Empty)
                    {
                        await CheckLimitAsync(session.AppId, pid);
                    }
                }

                // 3. Cleanup ALL other sessions
                var pidsToRemove = _currentSessionState.ActiveSessions.Keys.Where(k => k != activePid).ToList();
                foreach (var remotePid in pidsToRemove)
                {
                    if (_currentSessionState.ActiveSessions.TryRemove(remotePid, out var closedSession))
                    {
                        if (closedSession.DurationSeconds > 0)
                        {
                            await _repository.LogSessionAsync(closedSession);
                        }
                    }
                }

                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring loop");
                await Task.Delay(5000, stoppingToken);
            }
        }

        // Save all on exit
        foreach (var session in _currentSessionState.ActiveSessions.Values)
        {
            session.EndUtc = DateTime.UtcNow;
            session.DurationSeconds = (int)(session.EndUtc - session.StartUtc).TotalSeconds;
            await _repository.LogSessionAsync(session);
        }
    }

    private async Task<bool> CheckLimitAsync(Guid appId, int pid)
    {
        var rules = await _repository.GetRulesAsync();
        var rule = rules.FirstOrDefault(r => r.AppId == appId && r.Enabled);

        if (rule == null) return false;

        // Check Curfew
        if (rule.CurfewStartLocal.HasValue && rule.CurfewEndLocal.HasValue)
        {
            var now = DateTime.Now.TimeOfDay;
            var start = rule.CurfewStartLocal.Value;
            var end = rule.CurfewEndLocal.Value;
            bool inCurfew = false;

            if (start <= end)
            {
                inCurfew = now >= start && now <= end;
            }
            else
            {
                inCurfew = now >= start || now <= end;
            }

            if (inCurfew)
            {
                _logger.LogInformation($"Curfew active for {appId} ({start}-{end}).");
                _notificationQueue.Enqueue("Curfew Active", "Access is blocked during curfew hours.", "Warning");

                if (rule.ActionOnLimit == Core.Models.ActionOnLimit.ForceClose)
                {
                    NativeHelper.KillProcess(pid);
                }
                return true;
            }
        }

        if (rule.DailyLimitMinutes.HasValue)
        {
            var todayAggs = await _repository.GetAggregatesByDateAsync(DateTime.Now);
            var totalSeconds = todayAggs.Where(a => a.AppId == appId).Sum(a => a.TotalSeconds);

            // Add current session duration if it matches (approx)
            if (_currentSession != null && _currentSession.AppId == appId)
            {
                totalSeconds += (int)(DateTime.UtcNow - _currentSession.StartUtc).TotalSeconds;
            }

            if (totalSeconds > rule.DailyLimitMinutes.Value * 60)
            {
                if (rule.ActionOnLimit == Core.Models.ActionOnLimit.BlockNew || rule.ActionOnLimit == Core.Models.ActionOnLimit.ForceClose)
                {
                    _logger.LogInformation($"Limit enforcement: Killing {appId} (PID: {pid})");
                    bool killed = NativeHelper.KillProcess(pid);
                    if (killed)
                    {
                        var msg = $"You have used {rule.DailyLimitMinutes}m of {rule.DailyLimitMinutes}m. App closed.";
                        _notificationQueue.Enqueue("Time Limit Reached", msg, "Error");
                    }
                    else
                    {
                        _logger.LogError($"Failed to kill PID {pid}");
                    }
                    return true;
                }
                else if (rule.ActionOnLimit == Core.Models.ActionOnLimit.NotifyOnly)
                {
                    // Notify once per minute logic or simple enqueue (UI should deduplicate)
                    _logger.LogWarning($"Limit reached for {appId}, but NotifyOnly.");
                }
            }
        }
        return false;
    }

    private bool IsBrowser(string processName)
    {
        var name = processName.ToLower();
        return name == "chrome" || name == "msedge" || name == "firefox" || name == "opera";
    }

    private string DetermineCategory(string processName)
    {
        var name = processName.ToLower();
        if (name == "chrome" || name == "msedge" || name == "firefox" || name == "opera" || name == "brave") return "Browser";
        if (name == "devenv" || name == "code" || name == "idea" || name == "pycharm" || name == "rider") return "Development";
        if (name == "steam" || name == "dota2" || name == "csgo" || name == "league of legends") return "Games";
        if (name == "slack" || name == "discord" || name == "teams" || name == "zoom" || name == "skype") return "Communication";
        if (name == "winword" || name == "excel" || name == "powerpnt" || name == "outlook" || name == "onenote") return "Productivity";
        if (name == "spotify" || name == "vlc" || name == "mpc-hc") return "Media";
        return "Uncategorized";
    }

    private bool IsIgnored(string processName)
    {
        var name = processName.ToLower();
        return name == "idle" || name == "lockapp" || name == "searchui" || name == "shellexperiencehost" || name == "system";
    }

    private string? GetBrowserUrlFromAutomation(int pid, string processName)
    {
        // Note: UI Automation can be slow and resource intensive.
        // We should cache the Element or pattern if possible, but PIDs change.
        // Also, it requires the service to interact with desktop (Session 1), which Worker Service might not have if running as SYSTEM.
        // However, if running as user (Console/Tray), it works.
        // For System Service, we need "Interactive Services Detection" or similar, which is deprecated.
        // Assuming this app runs as User for now (Tray App or User Session Service).

        try
        {
            var element = AutomationElement.FromHandle(System.Diagnostics.Process.GetProcessById(pid).MainWindowHandle);
            if (element == null) return null;

            // Chrome/Edge usually put URL in "Address and search bar" edit control
            // We can search for Edit control with "Address and search bar" name or AccessKey "Ctrl+L"

            // Optimization: Use raw conditions
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var edit = element.FindFirst(TreeScope.Descendants, condition);

            // This is a naive search, Chrome has multiple edits. 
            // Better: Look for Name="Address and search bar"

            // This is a naive search, Chrome has multiple edits. 
            // Better: Look for Name="Address and search bar"
            // Chrome's address bar usually implements ValuePattern
            if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj))
            {
                var valuePattern = (ValuePattern)patternObj;
                var url = valuePattern.Current.Value;

                if (!string.IsNullOrEmpty(url))
                {
                    // Parse Host
                    if (!url.StartsWith("http")) url = "https://" + url;
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        return uri.Host;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private string? ParseDomainFromTitle(string title)
    {
        // Simple heuristic: "Page Title - Google Chrome"
        // Try to find known TLDs or just take the last part if it looks like a domain?
        // Actually, many browsers show "Page Title - Site Name"
        // It's hard to get exact domain without UI Automation or Extensions.
        // We will try to extract the "Site Name" part.

        if (string.IsNullOrWhiteSpace(title)) return null;

        var parts = title.Split(new[] { " - ", " — " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            // Usually the last part is the browser name or site name
            // Chrome: "GitHub - Where the world builds software - Google Chrome" -> last is Browser
            // So we might look at the second to last? 
            // This is very flaky. 
            // Let's just store the title and maybe try regex for "something.com" in the whole title.

            // Regex for domain
            var match = System.Text.RegularExpressions.Regex.Match(title, @"(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,6}");
            if (match.Success)
            {
                return match.Value;
            }
        }
        return null;
    }
}
