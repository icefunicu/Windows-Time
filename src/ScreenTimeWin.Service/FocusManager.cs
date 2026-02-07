using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Core.Models;
using ScreenTimeWin.Data;

namespace ScreenTimeWin.Service;

/// <summary>
/// 休息提醒事件参数
/// </summary>
public class BreakReminderEventArgs : EventArgs
{
    /// <summary>
    /// 建议休息时长（分钟）
    /// </summary>
    public int SuggestedBreakMinutes { get; init; }

    /// <summary>
    /// 已专注时长（分钟）
    /// </summary>
    public int FocusedMinutes { get; init; }

    /// <summary>
    /// 提醒消息
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 专注模式管理器 - 支持会话持久化和番茄钟休息提醒
/// </summary>
public class FocusManager
{
    private readonly DataRepository _repository;
    private FocusSession? _currentSession;
    private HashSet<Guid> _whitelistAppIds = new();
    private FocusType _focusType = FocusType.Whitelist;
    private Timer? _timer;
    private Timer? _breakTimer;

    #region 番茄钟休息提醒配置

    /// <summary>
    /// 休息提醒间隔（分钟），默认25分钟（番茄钟标准）
    /// </summary>
    public int BreakIntervalMinutes { get; set; } = 25;

    /// <summary>
    /// 休息时长（分钟），默认5分钟
    /// </summary>
    public int BreakDurationMinutes { get; set; } = 5;

    /// <summary>
    /// 是否启用休息提醒
    /// </summary>
    public bool BreakReminderEnabled { get; set; } = true;

    /// <summary>
    /// 休息提醒事件
    /// </summary>
    public event Action<BreakReminderEventArgs>? OnBreakReminder;

    #endregion

    // 原有属性
    public bool IsActive => _currentSession != null;
    public DateTime? EndTime => _currentSession?.EndLocal;

    // 新增属性供IpcServer访问
    public bool IsFocusActive => _currentSession != null;
    public DateTime? FocusStartTime => _currentSession?.StartLocal;
    public DateTime? FocusEndTime => _currentSession?.EndLocal;
    public int RemainingSeconds => _currentSession != null && _currentSession.EndLocal.HasValue
        ? Math.Max(0, (int)(_currentSession.EndLocal.Value - DateTime.Now).TotalSeconds)
        : 0;
    public IEnumerable<Guid> WhitelistAppIds => _whitelistAppIds;

    public FocusManager(DataRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 初始化时尝试恢复未完成的专注会话
    /// </summary>
    public async Task InitializeAsync()
    {
        var activeSession = await _repository.GetActiveFocusSessionAsync();
        if (activeSession != null)
        {
            _currentSession = activeSession;
            // 恢复计时器
            var remaining = activeSession.EndLocal!.Value - DateTime.Now;
            if (remaining.TotalSeconds > 0)
            {
                _timer?.Dispose();
                _timer = new Timer(OnFocusTimerExpired, null, remaining, Timeout.InfiniteTimeSpan);
            }
            else
            {
                // 会话已过期，清理
                await StopFocusAsync();
            }
        }
    }

    /// <summary>
    /// 开始专注模式并持久化到数据库
    /// </summary>
    public async Task StartFocusAsync(int durationMinutes, List<Guid> appIds, FocusType type, FocusMode mode = FocusMode.Normal)
    {
        _whitelistAppIds = new HashSet<Guid>(appIds);
        _focusType = type;
        var now = DateTime.Now;
        _currentSession = new FocusSession
        {
            StartLocal = now,
            DurationMinutes = durationMinutes,
            EndLocal = now.AddMinutes(durationMinutes),
            FocusMode = mode
        };

        // 持久化到数据库
        await _repository.SaveFocusSessionAsync(_currentSession);

        // Auto stop timer
        _timer?.Dispose();
        _timer = new Timer(OnFocusTimerExpired, null, TimeSpan.FromMinutes(durationMinutes), Timeout.InfiniteTimeSpan);

        // 启动番茄钟休息提醒定时器
        _breakTimer?.Dispose();
        if (BreakReminderEnabled && durationMinutes >= BreakIntervalMinutes)
        {
            _breakTimer = new Timer(OnBreakTimerElapsed, null,
                TimeSpan.FromMinutes(BreakIntervalMinutes),
                TimeSpan.FromMinutes(BreakIntervalMinutes));
        }
    }

    /// <summary>
    /// 同步版本（保持向后兼容）
    /// </summary>
    public void StartFocus(int durationMinutes, List<Guid> appIds, FocusType type, FocusMode mode = FocusMode.Normal)
    {
        StartFocusAsync(durationMinutes, appIds, type, mode).Wait();
    }

    /// <summary>
    /// 停止专注模式并更新数据库
    /// </summary>
    public async Task StopFocusAsync()
    {
        if (_currentSession != null)
        {
            // 更新数据库中的结束时间
            await _repository.UpdateFocusSessionAsync(_currentSession.Id, DateTime.Now);
        }

        _currentSession = null;
        _whitelistAppIds.Clear();
        _timer?.Dispose();
        _breakTimer?.Dispose();
    }

    /// <summary>
    /// 同步版本（保持向后兼容）
    /// </summary>
    public void StopFocus()
    {
        StopFocusAsync().Wait();
    }

    private void OnFocusTimerExpired(object? state)
    {
        _breakTimer?.Dispose();
        StopFocusAsync().Wait();
    }

    private void OnBreakTimerElapsed(object? state)
    {
        if (_currentSession == null) return;

        var focusedMinutes = (int)(DateTime.Now - _currentSession.StartLocal).TotalMinutes;
        OnBreakReminder?.Invoke(new BreakReminderEventArgs
        {
            SuggestedBreakMinutes = BreakDurationMinutes,
            FocusedMinutes = focusedMinutes,
            Message = $"您已专注 {focusedMinutes} 分钟，建议休息 {BreakDurationMinutes} 分钟！"
        });
    }

    public bool IsAllowed(Guid appId)
    {
        if (!IsActive) return true;
        if (_focusType == FocusType.Whitelist)
        {
            return _whitelistAppIds.Contains(appId);
        }
        else
        {
            // Blacklist: Allowed if NOT in the list
            return !_whitelistAppIds.Contains(appId);
        }
    }
}
