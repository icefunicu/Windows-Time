using ScreenTimeWin.Core.Entities;
using ScreenTimeWin.Core.Models;
using ScreenTimeWin.Data;

namespace ScreenTimeWin.Service;

public class FocusManager
{
    private readonly DataRepository _repository;
    private FocusSession? _currentSession;
    private HashSet<Guid> _whitelistAppIds = new();
    private Timer? _timer;

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

    public void StartFocus(int durationMinutes, List<Guid> whitelist, FocusMode mode = FocusMode.Normal)
    {
        _whitelistAppIds = new HashSet<Guid>(whitelist);
        var now = DateTime.Now;
        _currentSession = new FocusSession
        {
            StartLocal = now,
            DurationMinutes = durationMinutes,
            EndLocal = now.AddMinutes(durationMinutes),
            FocusMode = mode
        };

        // Auto stop timer
        _timer?.Dispose();
        _timer = new Timer(OnFocusTimerExpired, null, TimeSpan.FromMinutes(durationMinutes), Timeout.InfiniteTimeSpan);
    }

    public void StopFocus()
    {
        _currentSession = null;
        _whitelistAppIds.Clear();
        _timer?.Dispose();
    }

    private void OnFocusTimerExpired(object? state)
    {
        StopFocus();
    }

    public bool IsAllowed(Guid appId)
    {
        if (!IsActive) return true;
        return _whitelistAppIds.Contains(appId);
    }
}
